namespace CodeForCoders.Media.Application.Interfaces;

public sealed record VideoTranscriptionWriteScope(Guid VideoId, Guid ActorAccountId, string IdempotencyKey);
