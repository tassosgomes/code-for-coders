namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record VideoTranscriptionContentResponse(
    Guid VideoId, string Revision, string Language, long DurationMs, DateTimeOffset ReadyAt,
    IReadOnlyList<TranscriptSegmentResponse> Segments);
