using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetLessonTranscription;

public sealed record LessonTranscriptionOutput(Guid LessonId, string Status, string? Revision,
    string? Language, long? DurationMs, IReadOnlyList<TranscriptSegmentOutput> Segments);
