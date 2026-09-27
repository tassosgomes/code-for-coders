using System.Globalization;
using System.Text;
using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class Video
{
    public const int MaximumPreparationAttempts = 3;

    private Video()
    {
    }

    public Guid VideoId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string NormalizedTitle { get; private set; } = string.Empty;

    public Guid UploadedByAccountId { get; private set; }

    public string UploadedByName { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public int? DurationSeconds { get; private set; }

    public string? FailureReason { get; private set; }

    public string OriginalObjectKey { get; private set; } = string.Empty;

    public long OriginalSizeBytes { get; private set; }

    public long StoredBytes { get; private set; }

    public string? CorrelationId { get; private set; }

    public int PreparationAttempts { get; private set; }

    public DateTimeOffset? NextPreparationAt { get; private set; }

    public Guid? PreparationLeaseId { get; private set; }

    public DateTimeOffset? PreparationLeaseUntil { get; private set; }

    public byte[]? EncryptedVideoKey { get; private set; }

    public string? MasterKeyId { get; private set; }

    public DateTimeOffset? OriginalDeletedAt { get; private set; }

    public static Video Create(
        Guid tenantId,
        string title,
        Guid uploadedByAccountId,
        string uploadedByName,
        DateTimeOffset uploadedAt)
    {
        var videoId = Guid.CreateVersion7(uploadedAt);
        return Create(new VideoCreateInput(
            videoId,
            tenantId,
            title,
            uploadedByAccountId,
            uploadedByName,
            uploadedAt,
            $"{tenantId:D}/{videoId:D}/original",
            1,
            null));
    }

    public static Video Create(VideoCreateInput input)
    {
        if (input.VideoId == Guid.Empty || input.TenantId == Guid.Empty || input.UploadedByAccountId == Guid.Empty)
        {
            throw new EntityValidationException("Video, tenant, and uploader identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200)
        {
            throw new EntityValidationException("Video title must contain between 1 and 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.UploadedByName) || input.UploadedByName.Length > 200)
        {
            throw new EntityValidationException("Uploader name must contain between 1 and 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.OriginalObjectKey) || input.OriginalObjectKey.Length > 512 || input.OriginalSizeBytes < 1)
        {
            throw new EntityValidationException("Video source metadata is invalid.");
        }

        if (input.CorrelationId?.Length > 512)
        {
            throw new EntityValidationException("Video correlation identifier is invalid.");
        }

        return new Video
        {
            VideoId = input.VideoId,
            TenantId = input.TenantId,
            Title = input.Title.Trim(),
            NormalizedTitle = NormalizeTitle(input.Title),
            UploadedByAccountId = input.UploadedByAccountId,
            UploadedByName = input.UploadedByName.Trim(),
            UploadedAt = input.UploadedAt,
            Status = "received",
            OriginalObjectKey = input.OriginalObjectKey,
            OriginalSizeBytes = input.OriginalSizeBytes,
            CorrelationId = input.CorrelationId,
        };
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new EntityValidationException("Video title must contain between 1 and 200 characters.");
        }

        Title = title.Trim();
        NormalizedTitle = NormalizeTitle(title);
    }

    public static string NormalizeSearchTitle(string title) => NormalizeTitle(title);

    public void MarkPreparing(Guid leaseId, DateTimeOffset leaseUntil)
    {
        if (Status != "received" || leaseId == Guid.Empty)
        {
            throw new EntityValidationException("Only a received video can be claimed for preparation.");
        }

        Status = "preparing";
        PreparationLeaseId = leaseId;
        PreparationLeaseUntil = leaseUntil;
        NextPreparationAt = null;
        PreparationAttempts = checked(PreparationAttempts + 1);
    }

    public bool RenewPreparationLease(Guid leaseId, DateTimeOffset leaseUntil)
    {
        if (Status != "preparing" || PreparationLeaseId != leaseId)
        {
            return false;
        }

        PreparationLeaseUntil = leaseUntil;
        return true;
    }

    public void SchedulePreparationRetry(Guid leaseId, DateTimeOffset nextAttemptAt)
    {
        if (Status != "preparing" || PreparationLeaseId != leaseId || PreparationAttempts >= MaximumPreparationAttempts)
        {
            throw new EntityValidationException("The video preparation retry is invalid.");
        }

        Status = "received";
        FailureReason = null;
        PreparationLeaseId = null;
        PreparationLeaseUntil = null;
        NextPreparationAt = nextAttemptAt;
    }

    public void MarkFailed(Guid leaseId, string reason)
    {
        if (Status != "preparing" || PreparationLeaseId != leaseId)
        {
            throw new EntityValidationException("The video preparation lease is no longer active.");
        }

        if (reason is not (VideoFailureReasons.UnreadableFile
            or VideoFailureReasons.UnsupportedFormat
            or VideoFailureReasons.DurationExceeded
            or VideoFailureReasons.PreparationFailed))
        {
            throw new EntityValidationException("The video preparation failure reason is invalid.");
        }

        Status = "failed";
        FailureReason = reason;
        PreparationLeaseId = null;
        PreparationLeaseUntil = null;
        NextPreparationAt = null;
    }

    public void MarkReady(
        Guid leaseId,
        int durationSeconds,
        long storedBytes,
        byte[] encryptedVideoKey,
        string masterKeyId)
    {
        if (Status != "preparing" || PreparationLeaseId != leaseId)
        {
            throw new EntityValidationException("The video preparation lease is no longer active.");
        }

        if (durationSeconds < 1 || storedBytes < 1 || encryptedVideoKey.Length < 29 || string.IsNullOrWhiteSpace(masterKeyId))
        {
            throw new EntityValidationException("Video preparation result is invalid.");
        }

        Status = "ready";
        DurationSeconds = durationSeconds;
        StoredBytes = storedBytes;
        EncryptedVideoKey = encryptedVideoKey.ToArray();
        MasterKeyId = masterKeyId;
        PreparationLeaseId = null;
        PreparationLeaseUntil = null;
        NextPreparationAt = null;
    }

    public bool MarkOriginalDeleted(DateTimeOffset deletedAt)
    {
        if (Status is not ("ready" or "failed") || OriginalDeletedAt is not null)
        {
            return false;
        }

        OriginalDeletedAt = deletedAt;
        return true;
    }

    private static string NormalizeTitle(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString().Normalize(NormalizationForm.FormC);
    }
}
