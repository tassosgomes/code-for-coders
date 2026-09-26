namespace CodeForCoders.Media.Application.Interfaces;

/// <summary>
/// Vendor-neutral operations for private multipart media storage.
/// </summary>
public interface IMediaStoragePort
{
    Task<string> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(
        string objectKey,
        string storageUploadId,
        CancellationToken cancellationToken);

    Task<Uri> CreatePartUploadUriAsync(
        string objectKey,
        string storageUploadId,
        int partNumber,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task CompleteMultipartUploadAsync(
        string objectKey,
        string storageUploadId,
        IReadOnlyList<MediaUploadPart> parts,
        CancellationToken cancellationToken);

    Task AbortMultipartUploadAsync(
        string objectKey,
        string storageUploadId,
        CancellationToken cancellationToken);

    Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken);

    Task UploadDirectoryAsync(string sourceDirectory, string objectPrefix, CancellationToken cancellationToken);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);

    Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken);
}

public sealed record MediaUploadPart(int PartNumber, string ETag, long Size);
