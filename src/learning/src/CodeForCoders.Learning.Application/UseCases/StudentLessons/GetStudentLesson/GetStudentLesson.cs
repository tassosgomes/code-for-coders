using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.StudentLessons.GetStudentLesson;

public sealed class GetStudentLesson(IStudentLessonQueries lessons, IAccessDecisionClient access, ITenantContext tenant) : IGetStudentLesson
{
    public async Task<StudentLessonScreen> ExecuteAsync(GetStudentLessonInput input, CancellationToken cancellationToken)
    {
        var screen = await lessons.FindAsync(input.LessonId, cancellationToken)
            ?? throw new StudentLessonException("LESSON_NOT_AVAILABLE");
        var decision = await access.DecideAsync(new(tenant.TenantId!.Value, input.StudentId, screen.Course.CourseId), cancellationToken)
            ?? throw new StudentLessonException("ACCESS_DECISION_UNAVAILABLE");
        if (decision.Decision != "allowed")
            throw new StudentLessonException("ACCESS_DENIED", decision.DeniedReason, decision.LastExpiredAt);
        return screen;
    }
}
