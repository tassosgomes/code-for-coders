using System.Globalization;
using System.Text;
using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class Video
{
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

    public static Video Create(
        Guid tenantId,
        string title,
        Guid uploadedByAccountId,
        string uploadedByName,
        DateTimeOffset uploadedAt)
        => Create(Guid.CreateVersion7(uploadedAt), tenantId, title, uploadedByAccountId, uploadedByName, uploadedAt);

    public static Video Create(
        Guid videoId,
        Guid tenantId,
        string title,
        Guid uploadedByAccountId,
        string uploadedByName,
        DateTimeOffset uploadedAt)
    {
        if (videoId == Guid.Empty || tenantId == Guid.Empty || uploadedByAccountId == Guid.Empty)
        {
            throw new EntityValidationException("Video, tenant, and uploader identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new EntityValidationException("Video title must contain between 1 and 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(uploadedByName) || uploadedByName.Length > 200)
        {
            throw new EntityValidationException("Uploader name must contain between 1 and 200 characters.");
        }

        return new Video
        {
            VideoId = videoId,
            TenantId = tenantId,
            Title = title.Trim(),
            NormalizedTitle = NormalizeTitle(title),
            UploadedByAccountId = uploadedByAccountId,
            UploadedByName = uploadedByName.Trim(),
            UploadedAt = uploadedAt,
            Status = "received",
        };
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
