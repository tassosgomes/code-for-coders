using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Security;

/// <summary>
/// Signs the short-lived service assertion of the BFF. Each destination has its own audience, scopes and
/// private key (ADR-0004 for Identity, ADR-0009 for domain services); the tenant is the BFF's school.
/// </summary>
public sealed class ServiceAssertionTokenFactory(
    IOptions<StudentIdentityOptions> identityOptions,
    IOptions<CommerceServiceOptions> commerceOptions,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Create(string requiredScope) => Create(ServiceAssertionDestination.Identity, requiredScope);

    public string Create(ServiceAssertionDestination destination, string requiredScope)
    {
        var settings = Resolve(destination);
        if (!settings.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(requiredScope, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"The requested {destination} scope is not configured for the BFF.");
        }

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.SigningKeyBase64), out _);
        var now = timeProvider.GetUtcNow();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new AssertionHeader("RS256", "JWT", settings.SigningKeyId),
            JsonOptions));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new AssertionClaims(
                settings.Issuer,
                settings.Audience,
                settings.Issuer,
                Guid.Parse(settings.TenantId),
                requiredScope,
                Guid.CreateVersion7(now).ToString("D"),
                now.ToUnixTimeSeconds(),
                now.AddSeconds(-5).ToUnixTimeSeconds(),
                now.AddSeconds(30).ToUnixTimeSeconds()),
            JsonOptions));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");
        var signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64UrlEncode(signature)}";
    }

    private AssertionSettings Resolve(ServiceAssertionDestination destination)
    {
        var identity = identityOptions.Value;
        return destination switch
        {
            ServiceAssertionDestination.Identity => new AssertionSettings(
                identity.Issuer, identity.Audience, identity.Scope, identity.SigningKeyId, identity.SigningKeyBase64, identity.TenantId),
            ServiceAssertionDestination.Commerce => ResolveCommerce(identity.TenantId),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "Unknown service assertion destination."),
        };
    }

    private AssertionSettings ResolveCommerce(string tenantId)
    {
        var commerce = commerceOptions.Value;
        return new AssertionSettings(
            commerce.Issuer, commerce.Audience, commerce.Scope, commerce.SigningKeyId, commerce.SigningKeyBase64, tenantId);
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record AssertionSettings(
        string Issuer,
        string Audience,
        string Scope,
        string SigningKeyId,
        string SigningKeyBase64,
        string TenantId);

    private sealed record AssertionHeader(string Alg, string Typ, string Kid);

    private sealed record AssertionClaims(
        string Iss,
        string Aud,
        string Sub,
        Guid TenantId,
        string Scope,
        string Jti,
        long Iat,
        long Nbf,
        long Exp);
}
