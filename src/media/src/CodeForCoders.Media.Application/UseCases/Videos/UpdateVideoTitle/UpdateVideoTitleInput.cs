namespace CodeForCoders.Media.Application.UseCases.Videos.UpdateVideoTitle;

public sealed record UpdateVideoTitleInput(Guid VideoId, string Title, string IdempotencyKey);
