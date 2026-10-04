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
    IOptions<AwsMediaOptions> options) : IMediaStoragePort, IPlaybackPlaylistReader
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

    public Task<string> ReadPlaylistAsync(string objectKey, CancellationToken cancellationToken)
        => ExecuteAsync(async token =>
        {
            using var response = await clients.Internal.GetObjectAsync(new GetObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = GetKey(objectKey),
            }, token);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync(token);
        }, cancellationToken);

    public Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken)
        => ExecuteAsync(
            async token =>
            {
                using var response = await clients.Internal.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = options.Value.BucketName,
                    Key = GetKey(objectKey),
                }, token);
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await response.ResponseStream.CopyToAsync(destination, token);
            },
            cancellationToken);

    public async Task UploadDirectoryAsync(
        string sourceDirectory,
        string objectPrefix,
        CancellationToken cancellationToken)
    {
        var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
        foreach (var filePath in Directory.EnumerateFiles(fullSourceDirectory, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(fullSourceDirectory, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var objectKey = $"{objectPrefix.TrimEnd('/')}/{relativePath}";
            await ExecuteAsync(
                async token =>
                {
                    await using var stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await clients.Internal.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = options.Value.BucketName,
                        Key = GetKey(objectKey),
                        InputStream = stream,
                        CannedACL = S3CannedACL.Private,
                    }, token);
                },
                cancellationToken);
        }
    }

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
        => ExecuteAsync(
            token => clients.Internal.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = GetKey(objectKey),
            }, token),
            cancellationToken);

    public async Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var page = await ExecuteAsync(
                token => clients.Internal.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = options.Value.BucketName,
                    Prefix = GetKey(objectPrefix.TrimEnd('/') + "/"),
                    ContinuationToken = continuationToken,
                    MaxKeys = 1000,
                }, token),
                cancellationToken);
            var objectKeys = page.S3Objects?.Select(item => item.Key).ToArray() ?? [];
            if (objectKeys.Length > 0)
            {
                await ExecuteAsync(
                    token => clients.Internal.DeleteObjectsAsync(new DeleteObjectsRequest
                    {
                        BucketName = options.Value.BucketName,
                        Objects = objectKeys.Select(key => new KeyVersion { Key = key }).ToList(),
                        Quiet = true,
                    }, token),
                    cancellationToken);
            }

            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuationToken is not null);
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
