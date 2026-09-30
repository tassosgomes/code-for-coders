namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseVersion(Guid CourseId, int VersionNumber, string Title, string? Description,
    DateTimeOffset PublishedAt, CourseActor PublishedBy, string? VersionNote, bool Current, IReadOnlyList<CoursePublishedModule> Modules);
