using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Queries;

public sealed class CourseQueries(LearningDbContext dbContext) : ICourseQueries, IStudentLessonQueries
{
    public async Task<StudentLessonScreen?> FindAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var contains = System.Text.Json.JsonSerializer.Serialize(new[] { new { lessons = new[] { new { lessonId } } } });
        var version = await dbContext.CourseVersions.FromSqlInterpolated($"""
            SELECT v.* FROM content.course_versions v
            JOIN content.courses c ON c.id = v.course_id AND c.tenant_id = v.tenant_id
            WHERE c.current_version IS NOT NULL AND v.modules @> {contains}::jsonb
              AND NOT EXISTS (SELECT 1 FROM content.course_versions newer
                WHERE newer.tenant_id = v.tenant_id AND newer.course_id = v.course_id
                  AND newer.version_number > v.version_number)
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (version is null) return null;
        var module = version.Modules.Single(item => item.Lessons.Any(lesson => lesson.LessonId == lessonId));
        var lesson = module.Lessons.Single(item => item.LessonId == lessonId);
        return new(new(lesson.LessonId, module.ModuleId, lesson.Title, lesson.Position),
            new(version.CourseId, version.Title, version.VersionNumber,
                version.Modules.OrderBy(item => item.Position).ThenBy(item => item.ModuleId).Select(item =>
                    new StudentModuleOutline(item.ModuleId, item.Title, item.Position,
                        item.Lessons.OrderBy(entry => entry.Position).ThenBy(entry => entry.LessonId)
                            .Select(entry => new StudentLessonOutline(entry.LessonId, entry.Title, entry.Position)).ToArray())).ToArray()));
    }

    public async Task<IReadOnlyList<Guid>> PublishedIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => await dbContext.Courses.AsNoTracking().Where(course => ids.Contains(course.Id) && course.CurrentVersion != null)
            .Select(course => course.Id).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<CourseReference>> ResolveAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        var titles = await dbContext.Courses.AsNoTracking().Where(course => ids.Contains(course.Id))
            .ToDictionaryAsync(course => course.Id, course => course.Title, cancellationToken);
        return ids.Distinct().Select(id => new CourseReference(id, titles.GetValueOrDefault(id))).ToArray();
    }

    public async Task<CoursePage> ListAsync(CourseListQuery query, CancellationToken cancellationToken)
    {
        var courses = dbContext.Courses.AsNoTracking();
        if (query.Status is not null)
            courses = courses.Where(course => (course.CurrentVersion != null) == (query.Status == "published"));
        if (query.Title is not null)
        {
            var term = CourseTitleSearch.Normalize(query.Title).Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            courses = courses.Where(course => EF.Functions.Like(course.TitleSearch, "%" + term + "%", "\\"));
        }
        var total = await courses.LongCountAsync(cancellationToken);
        var data = await courses.OrderByDescending(course => course.LastEditedAt).ThenByDescending(course => course.Id)
            .Skip((query.Page - 1) * query.Size).Take(query.Size)
            .Select(course => new CourseSummary(course.Id, course.Title, course.CurrentVersion.HasValue ? "published" : "draft",
                course.CurrentVersion, course.HasUnpublishedChanges, course.LastEditedAt, new CourseActor(course.LastEditedByName), course.CurrentLevel))
            .ToListAsync(cancellationToken);
        return new CoursePage(data, new CoursePagination(query.Page, query.Size, total, (total + query.Size - 1) / query.Size));
    }
}
