namespace CodeForCoders.Media.Application.UseCases.Videos.RequestVideoTranscription;

public sealed record RequestVideoTranscriptionInput(Guid VideoId, string IdempotencyKey);
