using CodeForCoders.Media.Domain.Exceptions;
using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class VideoUpload
{
    public const long PartSizeBytes = 64L * 1024 * 1024;
    public const long MaxFileSizeBytes = 5L * 1024 * 1024 * 1024;

    private VideoUpload()
    {
    }

    public Guid UploadId { get; private set; }

    public Guid VideoId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UploaderAccountId { get; private set; }

    public string UploaderName { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public long FileSize { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public string Fingerprint { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public string StorageUploadId { get; private set; } = string.Empty;

    public int PartCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted => CompletedAt.HasValue;

    public static VideoUpload Create(
        Guid tenantId,
        Guid uploaderAccountId,
        string title,
        string fileName,
        long fileSize,
        string contentType,
        string fingerprint,
        string uploaderName,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new VideoUploadRuleViolationException("TITLE_REQUIRED", "A video title is required.");
        }

        if (fileSize > MaxFileSizeBytes)
        {
            throw new VideoUploadRuleViolationException("FILE_TOO_LARGE", "The selected file is larger than 5 GiB.");
        }

        if (!IsSupportedFormat(fileName, contentType))
        {
            throw new VideoUploadRuleViolationException("FORMAT_NOT_SUPPORTED", "Send an MP4, MOV, or MKV video.");
        }

        if (tenantId == Guid.Empty || uploaderAccountId == Guid.Empty)
        {
            throw new EntityValidationException("Tenant and uploader identifiers are required.");
        }

        if (fileSize < 1 || string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        {
            throw new EntityValidationException("Video file metadata is invalid.");
        }

        if (string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Length is < 16 or > 128)
        {
            throw new EntityValidationException("Video fingerprint must contain between 16 and 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(uploaderName) || uploaderName.Length > 200)
        {
            throw new EntityValidationException("Uploader name must contain between 1 and 200 characters.");
        }

        if (title.Length > 200)
        {
            throw new EntityValidationException("Video title cannot exceed 200 characters.");
        }

        var trimmedTitle = title.Trim();
        var partCount = checked((int)((fileSize + PartSizeBytes - 1) / PartSizeBytes));
        var videoId = Guid.CreateVersion7(createdAt);

        return new VideoUpload
        {
            UploadId = Guid.CreateVersion7(createdAt),
            VideoId = videoId,
            TenantId = tenantId,
            UploaderAccountId = uploaderAccountId,
            UploaderName = uploaderName.Trim(),
            Title = trimmedTitle,
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType.Trim().ToLowerInvariant(),
            Fingerprint = fingerprint,
            ObjectKey = $"{tenantId:D}/{videoId:D}/original",
            PartCount = partCount,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddHours(24),
        };
    }

    public void SetStorageUploadId(string storageUploadId)
    {
        if (string.IsNullOrWhiteSpace(storageUploadId))
        {
            throw new EntityValidationException("Storage upload identifier is required.");
        }

        StorageUploadId = storageUploadId;
    }

    public void ExtendExpiry(DateTimeOffset lastPartUrlExpiresAt)
    {
        if (IsCompleted)
        {
            throw new EntityValidationException("A completed upload cannot be extended.");
        }

        var nextExpiry = lastPartUrlExpiresAt.AddHours(24);
        if (nextExpiry > ExpiresAt)
        {
            ExpiresAt = nextExpiry;
        }
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        CompletedAt ??= completedAt;
    }

    private static bool IsSupportedFormat(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mediaType = contentType.Trim().ToLowerInvariant();
        return (extension, mediaType) is
            (".mp4", "video/mp4") or
            (".mov", "video/quicktime") or
            (".mkv", "video/x-matroska");
    }
}
