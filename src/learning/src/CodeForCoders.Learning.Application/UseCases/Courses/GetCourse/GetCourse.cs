using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Application.UseCases.Courses.GetCourse;

public sealed class GetCourse(ICourseRepository courses, ICourseQueries queries) : IGetCourse
{
    public async Task<CourseDetailOutput> ExecuteAsync(Guid input, CancellationToken cancellationToken)
    {
        var course = await courses.GetAsync(input, cancellationToken)
            ?? throw new NotFoundException("The requested course was not found.");
        var references = await queries.ResolveAsync(course.RecommendedCourseIds.ToArray(), cancellationToken);
        return CourseDetailOutput.FromCourse(course, references);
    }
}
