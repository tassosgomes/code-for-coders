using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class MediaStorageBoundaryTests
{
    [Fact]
    public async Task S3AdapterReturnsProviderReferenceAndCloudFrontUrlWithoutNetworkCall()
    {
        var options = Options.Create(new AwsMediaOptions
        {
            Region = "us-east-1",
            BucketName = "foundation-media",
            CloudFrontDistributionDomain = "cdn.example.test",
            ObjectKeyPrefix = "objects",
        });
        var cdn = new CloudFrontMediaCdnAdapter(options);
        var storage = new S3MediaStorageAdapter(options, cdn);

        var receipt = await storage.StageAsync(
            new("courses/intro.mp4", "video/mp4", 1024),
            TestContext.Current.CancellationToken);

        Assert.Equal("objects/courses/intro.mp4", receipt.ObjectKey);
        Assert.Equal("s3://foundation-media/objects/courses/intro.mp4", receipt.ProviderReference);
        Assert.Equal("https://cdn.example.test/objects/courses/intro.mp4", receipt.DeliveryUri.ToString());
    }
}
