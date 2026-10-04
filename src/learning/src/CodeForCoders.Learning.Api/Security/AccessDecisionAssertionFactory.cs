using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Api.Security;

public sealed class AccessDecisionAssertionFactory(IOptions<AccessDecisionOptions> options, TimeProvider clock)
{
    public string Create(Guid tenantId, string scope)
    {
        var settings = options.Value;
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.SigningKeyBase64), out _);
        var now = clock.GetUtcNow();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = settings.SigningKeyId }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = "learning",
            sub = "learning",
            aud = "commerce",
            tenantId,
            scope,
            jti = Guid.CreateVersion7(now).ToString("D"),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.AddSeconds(-5).ToUnixTimeSeconds(),
            exp = now.AddSeconds(30).ToUnixTimeSeconds()
        }));
        return $"{header}.{body}.{Encode(rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{body}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
