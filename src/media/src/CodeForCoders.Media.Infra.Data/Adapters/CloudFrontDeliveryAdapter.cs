using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class CloudFrontDeliveryAdapter(IOptions<PlaybackDeliveryOptions> options) : ISegmentDeliveryPort
{
    public SegmentDelivery CreateSegmentAccess(string videoPrefix, DateTimeOffset expiresAt)
    {
        var settings = options.Value;
        var baseAddress = new Uri(new Uri(settings.BaseAddress), videoPrefix);
        var policy = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Statement = new[] { new
            {
                Resource = baseAddress.AbsoluteUri + "*",
                Condition = new Dictionary<string, object>
                {
                    ["DateLessThan"] = new Dictionary<string, long> { ["AWS:EpochTime"] = expiresAt.ToUnixTimeSeconds() },
                },
            } },
        });
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.PrivateKeyBase64), out _);
        // The CloudFront RSA signed-URL protocol requires SHA-1, not the JWT signing algorithm.
#pragma warning disable CA5350
        var signature = rsa.SignData(policy, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
#pragma warning restore CA5350
        return new(baseAddress, new($"Policy={Encode(policy)}&Signature={Encode(signature)}&Key-Pair-Id={Uri.EscapeDataString(settings.KeyPairId)}", expiresAt));
    }

    private static string Encode(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('=', '_').Replace('/', '~');
}
