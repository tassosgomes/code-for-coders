namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseVersionSummary(int VersionNumber, DateTimeOffset PublishedAt, CourseActor PublishedBy, string? VersionNote, bool Current);
