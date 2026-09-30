using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseDetailOutput(
    Guid CourseId, string Title, string? Description, string Status, int? CurrentVersion,
    bool HasUnpublishedChanges, int DraftRevision, DateTimeOffset CreatedAt, CourseActor CreatedBy,
    DateTimeOffset LastEditedAt, CourseActor LastEditedBy, IReadOnlyList<object> Modules)
{
    public static CourseDetailOutput FromCourse(Course course)
        => new(course.Id, course.Title, course.Description, course.CurrentVersion.HasValue ? "published" : "draft",
            course.CurrentVersion, course.HasUnpublishedChanges, course.DraftRevision, course.CreatedAt,
            new CourseActor(course.CreatedByName), course.LastEditedAt, new CourseActor(course.LastEditedByName), []);
}
