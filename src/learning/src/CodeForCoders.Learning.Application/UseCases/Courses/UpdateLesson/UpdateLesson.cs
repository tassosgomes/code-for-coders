using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateLesson;

public sealed class UpdateLesson(CourseEditSession session) : IUpdateLesson
{
    public Task<CourseEditOutput> ExecuteAsync(UpdateLessonInput input, CancellationToken cancellationToken)
        => session.ExecuteWithVideoAsync(input.Context, $"update-lesson:{input.LessonId:D}", course => { course.UpdateLesson(input.LessonId, input.Changes); return null; }, input.Changes, cancellationToken);
}
