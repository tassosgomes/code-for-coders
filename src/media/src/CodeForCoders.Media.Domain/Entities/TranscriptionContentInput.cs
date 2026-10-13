namespace CodeForCoders.Media.Domain.Entities;

public sealed record TranscriptionContentInput(Guid TranscriptionId, Guid TenantId, string Language, long DurationMs, IReadOnlyList<TranscriptionSegment> Segments);
