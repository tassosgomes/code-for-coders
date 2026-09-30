using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateLesson;

public sealed class CreateLesson(CourseEditSession session) : ICreateLesson
{
    public Task<CourseEditOutput> ExecuteAsync(CreateLessonInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"create-lesson:{input.ModuleId:D}", course => { return course.AddLesson(input.ModuleId, input.Changes); }, cancellationToken);
}
