namespace CodeForCoders.Media.Application.Interfaces;

public sealed record SegmentAccess(string Query, DateTimeOffset ExpiresAt);
