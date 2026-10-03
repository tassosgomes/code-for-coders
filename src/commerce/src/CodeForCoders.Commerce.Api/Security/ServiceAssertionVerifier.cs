using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Api.Security;

/// <summary>
/// Verifies trusted service assertions (ADR-0009/0011): signature, <c>kid</c>, issuer,
/// audience, validity window, tenant and a single-use <c>jti</c>. Scope is reported, not enforced here.
/// </summary>
public sealed class ServiceAssertionVerifier(
    IOptions<ServiceAssertionOptions> options,
    IServiceAssertionReplayStore replayStore,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromSeconds(60);

    public async Task<VerifiedServiceAssertion?> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var segments = token.Split('.');
        if (segments.Length != 3 || !TryReadHeader(segments[0], out var keyId) || !TryReadClaims(segments[1], out var claims))
        {
            return null;
        }

        var settings = options.Value;
        if (!settings.Issuers.TryGetValue(claims.Issuer, out var issuer)
            || !issuer.PublicKeys.TryGetValue(keyId, out var encodedPublicKey)
            || !HasValidSignature(segments, encodedPublicKey))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (claims.Audience != settings.Audience
            || claims.Subject != claims.Issuer
            || claims.NotBefore > now + AllowedClockSkew
            || claims.IssuedAt > now + AllowedClockSkew
            || claims.ExpiresOn <= now
            || claims.ExpiresOn - claims.IssuedAt > MaximumLifetime
            || !issuer.AllowedTenantIds.Any(value => Guid.TryParse(value, out var allowed) && allowed == claims.TenantId))
        {
            return null;
        }

        if (!await replayStore.TryConsumeAsync(claims.AssertionId, claims.ExpiresOn, cancellationToken))
        {
            return null;
        }

        var granted = claims.Scopes
            .Where(scope => issuer.AllowedScopes.Contains(scope, StringComparer.Ordinal))
            .ToArray();
        return new VerifiedServiceAssertion(claims.Issuer, claims.TenantId, claims.AssertionId, claims.ExpiresOn, granted);
    }

    private static bool HasValidSignature(string[] segments, string encodedPublicKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(encodedPublicKey), out _);
            var signingInput = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
            return rsa.VerifyData(signingInput, Base64UrlDecode(segments[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadHeader(string encodedHeader, out string keyId)
    {
        keyId = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(Base64UrlDecode(encodedHeader));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("alg", out var algorithm)
                || algorithm.ValueKind != JsonValueKind.String
                || algorithm.GetString() != "RS256"
                || !root.TryGetProperty("kid", out var keyIdValue)
                || keyIdValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            keyId = keyIdValue.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(keyId);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadClaims(string encodedClaims, out AssertionClaims claims)
    {
        claims = default!;
        try
        {
            using var document = JsonDocument.Parse(Base64UrlDecode(encodedClaims));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var issuer = GetString(root, "iss");
            var audience = GetString(root, "aud");
            var subject = GetString(root, "sub");
            var tenant = GetString(root, "tenantId");
            var assertionId = GetString(root, "jti");
            var scope = GetString(root, "scope");
            if (issuer is null || audience is null || subject is null || tenant is null
                || assertionId is null || scope is null
                || !Guid.TryParse(tenant, out var tenantId) || tenantId == Guid.Empty
                || !Guid.TryParse(assertionId, out var parsedAssertionId) || parsedAssertionId == Guid.Empty
                || !TryReadUnixTime(root, "iat", out var issuedAt)
                || !TryReadUnixTime(root, "nbf", out var notBefore)
                || !TryReadUnixTime(root, "exp", out var expiresOn))
            {
                return false;
            }

            claims = new AssertionClaims(
                issuer,
                audience,
                subject,
                tenantId,
                parsedAssertionId,
                scope.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                issuedAt,
                notBefore,
                expiresOn);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryReadUnixTime(JsonElement root, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var element) || !element.TryGetInt64(out var timestamp))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        return true;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized,
        };
        return Convert.FromBase64String(normalized);
    }

    private sealed record AssertionClaims(
        string Issuer,
        string Audience,
        string Subject,
        Guid TenantId,
        Guid AssertionId,
        string[] Scopes,
        DateTimeOffset IssuedAt,
        DateTimeOffset NotBefore,
        DateTimeOffset ExpiresOn);
}
