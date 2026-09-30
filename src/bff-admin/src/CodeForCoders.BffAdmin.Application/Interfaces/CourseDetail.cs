namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseDetail(Guid CourseId, string Title, string? Description, string Status, int? CurrentVersion, bool HasUnpublishedChanges, int DraftRevision, DateTimeOffset CreatedAt, CourseActor CreatedBy, DateTimeOffset LastEditedAt, CourseActor LastEditedBy, IReadOnlyList<System.Text.Json.JsonElement> Modules, string? Level = null, string? CurrentLevel = null, CoursePrerequisite? Prerequisite = null);
