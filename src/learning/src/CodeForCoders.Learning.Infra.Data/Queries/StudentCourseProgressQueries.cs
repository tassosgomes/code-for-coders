using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Queries;

public sealed class StudentCourseProgressQueries(LearningDbContext db) : IStudentCourseProgressQueries
{
    private const int RestartBeforeEndSeconds = 10;

    public async Task<CurrentCourseProgressVersion?> FindCurrentVersionAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var version = await db.CourseVersions.FromSqlInterpolated($"""
            SELECT v.* FROM content.course_versions v
            JOIN content.courses c ON c.id = v.course_id AND c.tenant_id = v.tenant_id
              AND c.current_version = v.version_number
            WHERE v.course_id = {courseId}
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return version is null ? null : new(version.CourseId, version.VersionNumber,
            version.Modules.OrderBy(module => module.Position).ThenBy(module => module.ModuleId)
                .SelectMany(module => module.Lessons.OrderBy(lesson => lesson.Position).ThenBy(lesson => lesson.LessonId))
                .Select(lesson => new CurrentProgressLesson(lesson.LessonId, lesson.VideoId)).ToArray());
    }

    public async Task<StudentCourseProgress> ReadAsync(CurrentCourseProgressVersion version, Guid studentId, CancellationToken cancellationToken)
    {
        var lessonIds = version.Lessons.Select(lesson => lesson.LessonId).ToArray();
        var videoIds = version.Lessons.Select(lesson => lesson.VideoId).Distinct().ToArray();
        var progress = await db.LessonProgress.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.CourseId == version.CourseId && lessonIds.Contains(item.LessonId))
            .ToDictionaryAsync(item => item.LessonId, cancellationToken);
        var durations = await db.VideoDurations.AsNoTracking().Where(item => videoIds.Contains(item.VideoId))
            .ToDictionaryAsync(item => item.VideoId, item => item.DurationSeconds, cancellationToken);
        var lessons = version.Lessons.Where(lesson => progress.ContainsKey(lesson.LessonId)).Select(lesson =>
        {
            var item = progress[lesson.LessonId];
            var resume = item.Reason == "ended" || (durations.TryGetValue(lesson.VideoId, out var duration)
                && duration - item.LastPositionSeconds < RestartBeforeEndSeconds) ? 0 : item.LastPositionSeconds;
            return new StudentLessonProgress(lesson.LessonId, item.CompletedAt.HasValue, item.LastPositionSeconds, resume);
        }).ToArray();
        var completed = lessons.Count(lesson => lesson.Completed);
        var total = version.Lessons.Count;
        return new(version.CourseId, version.VersionNumber, completed, total, total == 0 ? 0 : completed * 100 / total, lessons);
    }
}
