namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CourseSummary(Guid CourseId, string Title, string Status, int? CurrentVersion, bool HasUnpublishedChanges, DateTimeOffset LastEditedAt, CourseActor LastEditedBy);
