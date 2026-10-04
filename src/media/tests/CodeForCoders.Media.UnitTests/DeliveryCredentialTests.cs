using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class DeliveryCredentialTests
{
    [Theory(DisplayName = nameof(CloudFrontPolicyIsSignedForExactlyOneVideoUntilSessionExpiry))]
    [InlineData("tenant/video/hls/")]
    [InlineData("tenant/other/hls/")]
    [InlineData("other/video/hls/")]
    public void CloudFrontPolicyIsSignedForExactlyOneVideoUntilSessionExpiry(string prefix)
    {
        using var key = RSA.Create(2048);
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        var adapter = new CloudFrontDeliveryAdapter(Options.Create(new PlaybackDeliveryOptions
        {
            BaseAddress = "https://cdn.test/",
            KeyPairId = "key-1",
            PrivateKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
        }));
        var access = adapter.CreateSegmentAccess(prefix, expires);
        var query = access.Access.Query.Split('&').Select(part => part.Split('=', 2)).ToDictionary(part => part[0], part => part[1]);
        var policy = Decode(query["Policy"]); var signature = Decode(query["Signature"]);
#pragma warning disable CA5350
        Assert.True(key.VerifyData(policy, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));
#pragma warning restore CA5350
        using var json = JsonDocument.Parse(policy);
        var statement = json.RootElement.GetProperty("Statement")[0];
        Assert.Equal("https://cdn.test/" + prefix + "*", statement.GetProperty("Resource").GetString());
        Assert.Equal(expires.ToUnixTimeSeconds(), statement.GetProperty("Condition").GetProperty("DateLessThan").GetProperty("AWS:EpochTime").GetInt64());
        Assert.Equal(expires, access.Access.ExpiresAt);
        Assert.Equal("key-1", query["Key-Pair-Id"]);
    }

    [Theory(DisplayName = nameof(DevelopmentCredentialBindsVideoAndExpiration))]
    [InlineData("tenant/video/hls/", 300)]
    [InlineData("tenant/other/hls/", 300)]
    [InlineData("tenant/video/hls/", -1)]
    public void DevelopmentCredentialBindsVideoAndExpiration(string prefix, int seconds)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(seconds);
        var adapter = new DevelopmentEdgeDeliveryAdapter(Options.Create(new PlaybackDeliveryOptions
        {
            BaseAddress = "http://edge.test/",
            SharedSecret = "development-test-secret",
        }));
        var access = adapter.CreateSegmentAccess(prefix, expires);
        Assert.Equal("http://edge.test/" + prefix, access.BaseAddress.AbsoluteUri);
        Assert.Equal(expires, access.Access.ExpiresAt);
        Assert.Contains("e=" + expires.ToUnixTimeSeconds(), access.Access.Query);
        var other = adapter.CreateSegmentAccess("other/video/hls/", expires);
        Assert.NotEqual(access.Access.Query, other.Access.Query);
        Assert.NotEqual(access.Access.Query, adapter.CreateSegmentAccess(prefix, expires.AddSeconds(1)).Access.Query);
    }

    private static byte[] Decode(string value)
        => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '=').Replace('~', '/'));
}
