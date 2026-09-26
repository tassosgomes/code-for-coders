namespace CodeForCoders.Media.Contracts;

public sealed record AtivoProntoV1(
    Guid EventId,
    Guid TenantId,
    Guid VideoId,
    DateTimeOffset OccurredAt,
    int DurationSeconds);
