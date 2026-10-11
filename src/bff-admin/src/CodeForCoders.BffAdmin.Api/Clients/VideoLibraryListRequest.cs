namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record VideoLibraryListRequest(
    int Page, int Size, IReadOnlyList<string> Statuses, string? Query,
    IReadOnlyList<string>? Tags = null, string TagMatch = "all");
