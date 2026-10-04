using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Progress.ListStudentCourses;

public sealed class ListStudentCourses(IStudentCoursesQueries queries, IStudentCourseAccessClient access,
    ITenantContext tenant) : IListStudentCourses
{
    public async Task<StudentCoursesOutput> ExecuteAsync(ListStudentCoursesInput input, CancellationToken cancellationToken)
    {
        var grants = await access.ListAsync(new(tenant.TenantId!.Value, input.StudentId), cancellationToken)
            ?? throw new StudentLessonException("COURSE_ACCESS_UNAVAILABLE");
        var ids = grants.Data.Select(grant => grant.CourseId).ToArray();
        var versions = await queries.ListCurrentVersionsAsync(ids, cancellationToken);
        var progress = await queries.ListProgressAsync(input.StudentId, versions.Select(version => version.CourseId).ToArray(), cancellationToken);
        var activity = progress.ToLookup(item => item.CourseId);
        var courses = versions.Where(version => version.LessonIds.Count > 0).ToDictionary(version => version.CourseId);
        var active = grants.Data.Where(grant => grant.Status == "active" && courses.ContainsKey(grant.CourseId))
            .Select(grant => (Grant: grant, Course: CreateActive(courses[grant.CourseId], activity[grant.CourseId].ToArray())))
            .OrderByDescending(item => item.Course.LastActivityAt).ThenByDescending(item => item.Grant.Since).ThenBy(item => item.Course.CourseId)
            .Select(item => item.Course).ToArray();
        var ended = grants.Data.Where(grant => grant.Status == "ended" && courses.ContainsKey(grant.CourseId))
            .OrderByDescending(grant => grant.EndedAt).ThenBy(grant => grant.CourseId)
            .Select(grant => new EndedStudentCourse(grant.CourseId, courses[grant.CourseId].Title,
                grant.EndedOn!.Value, grant.EndedReason!, Summary(courses[grant.CourseId], activity[grant.CourseId].ToArray()))).ToArray();
        return new(true, active, ended);
    }

    private static ActiveStudentCourse CreateActive(CurrentStudentCourse course, IReadOnlyList<StudentCourseLessonActivity> progress)
    {
        var latest = progress.OrderByDescending(item => item.LastActivityAt).ThenBy(item => item.LessonId).FirstOrDefault();
        return new(course.CourseId, course.Title, latest is not null, latest?.LastActivityAt,
            ContinueLesson(course, progress, latest), Summary(course, progress));
    }

    private static StudentCourseProgressSummary Summary(CurrentStudentCourse course, IReadOnlyList<StudentCourseLessonActivity> progress)
    {
        var completed = progress.Count(item => item.CompletedAt.HasValue && course.LessonIds.Contains(item.LessonId));
        var total = course.LessonIds.Count;
        return new(completed, total, total == 0 ? 0 : completed * 100 / total);
    }

    private static Guid ContinueLesson(CurrentStudentCourse course, IReadOnlyList<StudentCourseLessonActivity> progress, StudentCourseLessonActivity? latest)
    {
        var lessons = course.LessonIds;
        var completed = progress.Where(item => item.CompletedAt.HasValue).Select(item => item.LessonId).ToHashSet();
        if (latest is null) return lessons[0];
        var index = Array.IndexOf(lessons.ToArray(), latest.LessonId);
        if (index >= 0 && !completed.Contains(latest.LessonId)) return latest.LessonId;
        var remaining = lessons.Skip(index + 1).FirstOrDefault(lesson => !completed.Contains(lesson));
        if (remaining != Guid.Empty) return remaining;
        remaining = lessons.FirstOrDefault(lesson => !completed.Contains(lesson));
        return remaining != Guid.Empty ? remaining : index >= 0 ? latest.LessonId : lessons[0];
    }
}
