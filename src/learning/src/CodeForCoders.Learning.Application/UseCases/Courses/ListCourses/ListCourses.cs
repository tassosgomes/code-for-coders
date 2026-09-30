using CodeForCoders.Learning.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ListCourses;

public sealed class ListCourses(ICourseQueries queries) : IListCourses
{
    public Task<CoursePage> ExecuteAsync(CourseListQuery input, CancellationToken cancellationToken)
    {
        if (input.Page < 1 || input.Size is < 1 or > 50 || (long)(input.Page - 1) * input.Size > int.MaxValue
            || input.Status is not (null or "draft" or "published"))
            throw new ValidationException("The requested page is invalid.");
        return queries.ListAsync(input, cancellationToken);
    }
}
