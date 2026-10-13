namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionContentSnapshot(
    Guid TranscriptionId, string Language, long DurationMs, DateTimeOffset ReadyAt, string SegmentsJson);
