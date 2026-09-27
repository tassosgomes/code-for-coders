namespace CodeForCoders.Media.Contracts;

public sealed record PreparacaoFalhouV1(
    Guid EventId,
    Guid TenantId,
    Guid VideoId,
    DateTimeOffset OccurredAt,
    string Reason);
