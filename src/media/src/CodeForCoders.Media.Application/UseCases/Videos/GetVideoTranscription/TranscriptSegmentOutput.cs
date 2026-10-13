namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideoTranscription;

public sealed record TranscriptSegmentOutput(long StartMs, long EndMs, string Text);
