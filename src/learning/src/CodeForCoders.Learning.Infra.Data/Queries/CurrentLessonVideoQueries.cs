using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Queries;

public sealed class CurrentLessonVideoQueries(LearningDbContext dbContext) : ICurrentLessonVideoQueries
{
    public async Task<Guid?> FindVideoAsync(CurrentLessonReference lesson, CancellationToken cancellationToken)
    {
        var version = await CurrentVersions().Where(version => version.CourseId == lesson.CourseId)
            .SingleOrDefaultAsync(cancellationToken);
        return version?.Modules.SelectMany(module => module.Lessons)
            .SingleOrDefault(entry => entry.LessonId == lesson.LessonId)?.VideoId;
    }

    public async Task<IReadOnlyList<CurrentLessonReference>> ListByVideoAsync(Guid videoId, CancellationToken cancellationToken)
    {
        var contains = System.Text.Json.JsonSerializer.Serialize(new[] { new { lessons = new[] { new { videoId } } } });
        var versions = await CurrentVersions(contains).ToListAsync(cancellationToken);
        return versions.SelectMany(version => version.Modules.SelectMany(module => module.Lessons)
            .Where(lesson => lesson.VideoId == videoId)
            .Select(lesson => new CurrentLessonReference(version.CourseId, lesson.LessonId))).ToArray();
    }

    private IQueryable<CourseVersion> CurrentVersions(string contains = "[]") => dbContext.CourseVersions
        .FromSqlInterpolated($"""
            SELECT v.* FROM content.course_versions v
            JOIN content.courses c ON c.id = v.course_id AND c.tenant_id = v.tenant_id
              AND c.current_version = v.version_number
            WHERE v.modules @> {contains}::jsonb
            """).AsNoTracking();
}
