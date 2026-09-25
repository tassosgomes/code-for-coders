using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class IdentityJwksMessageHandler : HttpMessageHandler
{
    public const string KeyId = "commerce-test-key";
    private readonly RSA signingRsa = RSA.Create(2048);

    public bool IsUnavailable { get; set; }

    public SecurityKey SigningKey => new RsaSecurityKey(signingRsa) { KeyId = KeyId };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (IsUnavailable)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        }

        var parameters = signingRsa.ExportParameters(includePrivateParameters: false);
        var body = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncode(parameters.Modulus!),
                    e = Base64UrlEncode(parameters.Exponent!),
                },
            },
        });
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            signingRsa.Dispose();
        }

        base.Dispose(disposing);
    }
}
