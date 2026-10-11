namespace CodeForCoders.Media.Application.UseCases.Videos.ReplaceVideoTags;

public sealed record ReplaceVideoTagsInput(Guid VideoId, IReadOnlyList<string>? Tags, string IdempotencyKey);
