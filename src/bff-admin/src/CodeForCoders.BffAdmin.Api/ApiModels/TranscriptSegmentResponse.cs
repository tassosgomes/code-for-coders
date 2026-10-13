namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record TranscriptSegmentResponse(long StartMs, long EndMs, string Text);
