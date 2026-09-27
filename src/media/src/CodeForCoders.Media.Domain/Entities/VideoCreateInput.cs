namespace CodeForCoders.Media.Domain.Entities;

public sealed record VideoCreateInput(
    Guid VideoId,
    Guid TenantId,
    string Title,
    Guid UploadedByAccountId,
    string UploadedByName,
    DateTimeOffset UploadedAt,
    string OriginalObjectKey,
    long OriginalSizeBytes,
    string? CorrelationId);
