using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Security;

public sealed class ServiceAssertionTokenFactory(
    IOptions<StaffIdentityOptions> options,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedScopes =
    [
        "staff-passwords:reset",
        "staff-sessions:create",
        "staff-sessions:validate",
        "staff-sessions:revoke",
        "staff-invitations:read",
        "staff-invitations:write",
    ];

    public string Create(string requiredScope)
    {
        if (!AllowedScopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The requested Identity scope is not configured for the BFF.");
        }

        var settings = options.Value;
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

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
