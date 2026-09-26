using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Adapters;

public sealed class S3MediaStorageAdapter(
    S3MediaClientPair clients,
    IOptions<AwsMediaOptions> options) : IMediaStoragePort
{
    public async Task<string> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(
            cancellationToken => clients.Internal.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = options.Value.BucketName,
                Key = GetKey(objectKey),
                ContentType = contentType,
                CannedACL = S3CannedACL.Private,
            }, cancellationToken),
            cancellationToken);

        return response.UploadId;
    }

    public async Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(
        string objectKey,
        string storageUploadId,
        CancellationToken cancellationToken)
    {
        var parts = new List<MediaUploadPart>();
        string? marker = null;
        bool isTruncated;
        do
        {
            var response = await ExecuteAsync(
                token => clients.Internal.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = GetKey(objectKey),
                    UploadId = storageUploadId,
                    PartNumberMarker = marker,
                    MaxParts = 1000,
                }, token),
                cancellationToken);

            if (response.Parts is not null)
            {
                parts.AddRange(response.Parts
                    .Where(part => part.PartNumber.HasValue && part.Size.HasValue && !string.IsNullOrWhiteSpace(part.ETag))
                    .Select(part => new MediaUploadPart(part.PartNumber!.Value, part.ETag!, part.Size!.Value)));
            }
            marker = response.NextPartNumberMarker?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            isTruncated = response.IsTruncated ?? false;
        }
        while (isTruncated);

        return parts.OrderBy(part => part.PartNumber).ToArray();
    }

    public Task<Uri> CreatePartUploadUriAsync(
        string objectKey,
        string storageUploadId,
        int partNumber,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var url = clients.Public.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = options.Value.BucketName,
                Key = GetKey(objectKey),
                UploadId = storageUploadId,
                PartNumber = partNumber,
                Protocol = UseHttpForPublicEndpoint() ? Protocol.HTTP : Protocol.HTTPS,
                Verb = HttpVerb.PUT,
                Expires = expiresAt.UtcDateTime,
            });
            return Task.FromResult(new Uri(url, UriKind.Absolute));
        }
        catch (AmazonServiceException)
        {
            throw new StorageUnavailableException();
        }
    }

    public async Task CompleteMultipartUploadAsync(
        string objectKey,
        string storageUploadId,
        IReadOnlyList<MediaUploadPart> parts,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            token => clients.Internal.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = options.Value.BucketName,
                Key = GetKey(objectKey),
                UploadId = storageUploadId,
                PartETags = parts.Select(part => new PartETag(part.PartNumber, part.ETag)).ToList(),
            }, token),
            cancellationToken);
    }

    public async Task AbortMultipartUploadAsync(
        string objectKey,
        string storageUploadId,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(
                token => clients.Internal.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = GetKey(objectKey),
                    UploadId = storageUploadId,
                }, token),
                cancellationToken);
        }
        catch (MultipartUploadNotFoundException)
        {
            // Aborting an upload that is already absent is an idempotent cleanup operation.
        }
    }

    private string GetKey(string objectKey)
    {
        var prefix = options.Value.ObjectKeyPrefix.Trim('/');
        var normalizedObjectKey = objectKey.Trim('/');
        return string.IsNullOrEmpty(prefix) ? normalizedObjectKey : $"{prefix}/{normalizedObjectKey}";
    }

    private bool UseHttpForPublicEndpoint()
        => Uri.TryCreate(options.Value.EndpointPublic, UriKind.Absolute, out var endpoint)
            && endpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutToken(cancellationToken);
        try
        {
            return await operation(timeout.Token);
        }
        catch (AmazonS3Exception exception) when (string.Equals(exception.ErrorCode, "NoSuchUpload", StringComparison.Ordinal))
        {
            throw new MultipartUploadNotFoundException();
        }
        catch (AmazonServiceException)
        {
            throw new StorageUnavailableException();
        }
        catch (HttpRequestException)
        {
            throw new StorageUnavailableException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StorageUnavailableException();
        }
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeoutToken(cancellationToken);
        try
        {
            await operation(timeout.Token);
        }
        catch (AmazonS3Exception exception) when (string.Equals(exception.ErrorCode, "NoSuchUpload", StringComparison.Ordinal))
        {
            throw new MultipartUploadNotFoundException();
        }
        catch (AmazonServiceException)
        {
            throw new StorageUnavailableException();
        }
        catch (HttpRequestException)
        {
            throw new StorageUnavailableException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StorageUnavailableException();
        }
    }

    private CancellationTokenSource CreateTimeoutToken(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));
        return timeout;
    }
}
