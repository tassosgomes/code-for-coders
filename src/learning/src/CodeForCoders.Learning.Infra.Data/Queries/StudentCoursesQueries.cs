using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Queries;

public sealed class StudentCoursesQueries(LearningDbContext db) : IStudentCoursesQueries
{
    public async Task<IReadOnlyList<CurrentStudentCourse>> ListCurrentVersionsAsync(IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken)
    {
        var ids = courseIds.ToArray();
        var versions = await db.CourseVersions.FromSqlInterpolated($"""
            SELECT v.* FROM content.course_versions v
            JOIN content.courses c ON c.id = v.course_id AND c.tenant_id = v.tenant_id
              AND c.current_version = v.version_number
            WHERE v.course_id = ANY({ids})
            """).AsNoTracking().ToListAsync(cancellationToken);
        return versions.Select(version => new CurrentStudentCourse(version.CourseId, version.Title,
            version.Modules.OrderBy(module => module.Position).ThenBy(module => module.ModuleId)
                .SelectMany(module => module.Lessons.OrderBy(lesson => lesson.Position).ThenBy(lesson => lesson.LessonId))
                .Select(lesson => lesson.LessonId).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<StudentCourseLessonActivity>> ListProgressAsync(Guid studentId, IReadOnlyList<Guid> courseIds, CancellationToken cancellationToken)
        => await db.LessonProgress.AsNoTracking().Where(item => item.StudentId == studentId && courseIds.Contains(item.CourseId))
            .Select(item => new StudentCourseLessonActivity(item.CourseId, item.LessonId, item.LastActivityAt, item.CompletedAt))
            .ToListAsync(cancellationToken);
}
