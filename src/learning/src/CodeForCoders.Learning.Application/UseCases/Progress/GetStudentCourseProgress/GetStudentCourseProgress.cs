using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Progress.GetStudentCourseProgress;

public sealed class GetStudentCourseProgress(IStudentCourseProgressQueries queries, IAccessDecisionClient access, ITenantContext tenant) : IGetStudentCourseProgress
{
    public async Task<StudentCourseProgress> ExecuteAsync(GetStudentCourseProgressInput input, CancellationToken cancellationToken)
    {
        var version = await queries.FindCurrentVersionAsync(input.CourseId, cancellationToken)
            ?? throw new StudentLessonException("COURSE_NOT_AVAILABLE");
        var decision = await access.DecideAsync(new(tenant.TenantId!.Value, input.StudentId, version.CourseId), cancellationToken)
            ?? throw new StudentLessonException("ACCESS_DECISION_UNAVAILABLE");
        if (decision.Decision != "allowed")
            throw new StudentLessonException("ACCESS_DENIED", decision.DeniedReason, decision.LastExpiredAt);
        return await queries.ReadAsync(version, input.StudentId, cancellationToken);
    }
}
