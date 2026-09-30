using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Repositories;

namespace CodeForCoders.Learning.Application.UseCases.Courses.GetCourse;

public sealed class GetCourse(ICourseRepository courses) : IGetCourse
{
    public async Task<CourseDetailOutput> ExecuteAsync(Guid input, CancellationToken cancellationToken)
    {
        var course = await courses.GetAsync(input, cancellationToken)
            ?? throw new NotFoundException("The requested course was not found.");
        return CourseDetailOutput.FromCourse(course);
    }
}
