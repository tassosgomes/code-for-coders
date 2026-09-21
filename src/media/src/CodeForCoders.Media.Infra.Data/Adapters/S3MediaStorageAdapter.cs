using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

/// <summary>
/// Anti-corruption adapter for S3 object identity. The technical slice deliberately does not call AWS.
/// A later implementation can add the AWS SDK here without changing Application ports or contracts.
/// </summary>
public sealed class S3MediaStorageAdapter(
    IOptions<AwsMediaOptions> options,
    IMediaCdnPort cdnPort) : IMediaStoragePort
{
    public Task<MediaObjectReceipt> StageAsync(
        MediaObjectRequest request,
        CancellationToken cancellationToken)
    {
        var prefix = options.Value.ObjectKeyPrefix.Trim('/');
        var objectKey = string.IsNullOrEmpty(prefix)
            ? request.ObjectKey.Trim('/')
            : $"{prefix}/{request.ObjectKey.Trim('/')}";
        var providerReference = $"s3://{options.Value.BucketName}/{objectKey}";

        return Task.FromResult(new MediaObjectReceipt(
            objectKey,
            providerReference,
            cdnPort.CreateDeliveryUri(objectKey)));
    }
}
