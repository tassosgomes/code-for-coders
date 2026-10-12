namespace CodeForCoders.Media.Domain.Entities;

public sealed record TranscriptionSegment(long StartMs, long EndMs, string Text);
