namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideos;

public sealed record ListVideosInput(int Page, int Size, IReadOnlyList<string> Statuses, string? Query);
