namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record PublishedCourseSnapshot(
    Guid TenantId, Guid CourseId, int VersionNumber, string SourceFormat,
    string Title, string Description, string? Level, string PrerequisiteJson,
    string StructureJson, DateTimeOffset PublishedAt);
