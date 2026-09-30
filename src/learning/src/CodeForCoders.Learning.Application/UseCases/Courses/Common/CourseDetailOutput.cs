using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseDetailOutput(
    Guid CourseId, string Title, string? Description, string Status, int? CurrentVersion,
    bool HasUnpublishedChanges, int DraftRevision, DateTimeOffset CreatedAt, CourseActor CreatedBy,
    DateTimeOffset LastEditedAt, CourseActor LastEditedBy, IReadOnlyList<CourseModuleOutput> Modules)
{
    public static CourseDetailOutput FromCourse(Course course)
        => new(course.Id, course.Title, course.Description, course.CurrentVersion.HasValue ? "published" : "draft",
            course.CurrentVersion, course.HasUnpublishedChanges, course.DraftRevision, course.CreatedAt,
            new CourseActor(course.CreatedByName), course.LastEditedAt, new CourseActor(course.LastEditedByName), course.Modules.OrderBy(module => module.Position).Select(module =>
                new CourseModuleOutput(module.Id, module.Title, module.Position, module.Lessons.OrderBy(lesson => lesson.Position)
                    .Select(lesson => new CourseLessonOutput(lesson.Id, lesson.Title, lesson.Description, lesson.Position, lesson.VideoId.HasValue ? new CourseVideoOutput(lesson.VideoId.Value) : null)).ToList())).ToList());
}
