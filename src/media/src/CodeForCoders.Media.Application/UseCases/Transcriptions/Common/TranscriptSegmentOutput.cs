namespace CodeForCoders.Media.Application.UseCases.Transcriptions.Common;

public sealed record TranscriptSegmentOutput(long StartMs, long EndMs, string Text);
