namespace CodeForCoders.Media.Application.Interfaces;

public sealed record VideoListQuery(
    int Page, int Size, IReadOnlyList<string> Statuses, string? Query,
    IReadOnlyList<string> Tags, string TagMatch);
