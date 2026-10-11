namespace CodeForCoders.Media.Application.Interfaces;

public sealed record VideoTagWriteScope(Guid VideoId, Guid ActorAccountId, string IdempotencyKey);
