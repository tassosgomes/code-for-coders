using System.Security.Cryptography;
using CodeForCoders.Identity.Api.ApiModels;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Api.Security;

public sealed class UserTokenSigningKeySet(IOptions<StaffSessionTokenOptions> options)
{
    public JsonWebKeySetResponse GetPublicKeys()
    {
        var settings = options.Value;
        var keys = new List<JsonWebKeyResponse>();
        if (!string.IsNullOrWhiteSpace(settings.SigningKeyId)
            && !string.IsNullOrWhiteSpace(settings.SigningKeyBase64))
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.SigningKeyBase64), out _);
            keys.Add(ToJsonWebKey(settings.SigningKeyId, rsa.ExportParameters(includePrivateParameters: false)));
        }

        foreach (var previousKey in settings.PreviousSigningPublicKeys)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(previousKey.Value), out _);
            keys.Add(ToJsonWebKey(previousKey.Key, rsa.ExportParameters(includePrivateParameters: false)));
        }

        return new JsonWebKeySetResponse(keys);
    }

    private static JsonWebKeyResponse ToJsonWebKey(string keyId, RSAParameters parameters)
        => new(
            "RSA",
            "sig",
            "RS256",
            keyId,
            Base64UrlEncode(parameters.Modulus!),
            Base64UrlEncode(parameters.Exponent!));

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
