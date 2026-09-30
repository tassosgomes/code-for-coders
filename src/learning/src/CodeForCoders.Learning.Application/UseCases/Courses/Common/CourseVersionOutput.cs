using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseVersionOutput(Guid CourseId, int VersionNumber, string Title,
    string? Description, DateTimeOffset PublishedAt, CourseActor PublishedBy, string? VersionNote,
    bool Current, IReadOnlyList<CourseVersionModuleOutput> Modules, string? Level, CoursePrerequisiteOutput Prerequisite)
{
    public static CourseVersionOutput FromVersion(CourseVersion version, bool current = true)
        => new(version.CourseId, version.VersionNumber, version.Title, version.Description,
            version.PublishedAt, new(version.PublishedByName), version.VersionNote, current,
            version.Modules.Select(module => new CourseVersionModuleOutput(module.ModuleId, module.Title, module.Position,
                module.Lessons.Select(lesson => new CourseVersionLessonOutput(lesson.LessonId, lesson.Title, lesson.Description, lesson.Position, lesson.VideoId)).ToArray())).ToArray(),
            version.Level, new(version.Prerequisite?.Text,
                version.Prerequisite?.RecommendedCourses.Select(item => new RecommendedCourseOutput(item.CourseId, item.Title)).ToArray() ?? []));
}
