using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Api.Security;

public sealed class StaffSessionTokenIssuer(
    IOptions<StaffSessionTokenOptions> options,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanIssueFor(string? audience)
        => !string.IsNullOrWhiteSpace(audience)
            && options.Value.AudienceScopes.ContainsKey(audience);

    public string Create(
        string audience,
        Guid tenantId,
        Guid accountId,
        Guid sessionId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions)
    {
        var settings = options.Value;
        if (!settings.AudienceScopes.TryGetValue(audience, out var scope))
        {
            throw new InvalidOperationException("The requested staff session audience is not configured.");
        }

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.SigningKeyBase64), out _);
        var now = timeProvider.GetUtcNow();
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new TokenHeader("RS256", "JWT", settings.SigningKeyId), JsonOptions));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new TokenClaims(
                settings.Issuer,
                audience,
                accountId.ToString("D"),
                tenantId.ToString("D"),
                sessionId.ToString("D"),
                scope,
                roles,
                permissions,
                now.ToUnixTimeSeconds(),
                now.AddSeconds(-5).ToUnixTimeSeconds(),
                now.AddMinutes(settings.LifetimeMinutes).ToUnixTimeSeconds(),
                Guid.CreateVersion7(now).ToString("D")),
            JsonOptions));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");
        var signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenHeader(string Alg, string Typ, string Kid);

    private sealed record TokenClaims(
        string Iss,
        string Aud,
        string Sub,
        string TenantId,
        string SessionId,
        string Scope,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        long Iat,
        long Nbf,
        long Exp,
        string Jti);
}
