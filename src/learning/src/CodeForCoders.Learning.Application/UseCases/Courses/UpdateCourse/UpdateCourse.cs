using CodeForCoders.Learning.Application.UseCases.Courses.Common;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateCourse;

public sealed class UpdateCourse(CourseEditSession session) : IUpdateCourse
{
    public Task<CourseEditOutput> ExecuteAsync(UpdateCourseInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, $"update-course", course => { course.Update(input.Changes); return null; }, cancellationToken);
}
