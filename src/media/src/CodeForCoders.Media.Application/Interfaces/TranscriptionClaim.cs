namespace CodeForCoders.Media.Application.Interfaces;

public sealed record TranscriptionClaim(DateTimeOffset Now, TimeSpan LeaseDuration, bool CanExtract);
