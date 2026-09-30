using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DeleteLesson;

public sealed class DeleteLesson(CourseEditSession session) : IDeleteLesson
{
    public Task<CourseEditOutput> ExecuteAsync(DeleteLessonInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"delete-lesson:{input.LessonId:D}", course => { course.RemoveLesson(input.LessonId); return null; }, cancellationToken);
}
