namespace CodeForCoders.Media.Domain.Entities;

public sealed record TranscriptionCreateInput(Guid TenantId, Guid VideoId, string Origin, Guid? RequestedByAccountId, DateTimeOffset RequestedAt);
