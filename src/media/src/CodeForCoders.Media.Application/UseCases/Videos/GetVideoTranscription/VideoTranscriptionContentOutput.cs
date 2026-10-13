using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;

namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideoTranscription;

public sealed record VideoTranscriptionContentOutput(
    Guid VideoId, string Revision, string Language, long DurationMs, DateTimeOffset ReadyAt,
    IReadOnlyList<TranscriptSegmentOutput> Segments);
