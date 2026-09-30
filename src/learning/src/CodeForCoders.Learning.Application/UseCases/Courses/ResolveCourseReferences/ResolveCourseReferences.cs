using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.SeedWork;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ResolveCourseReferences;

public sealed class ResolveCourseReferences(ICourseQueries queries) : IResolveCourseReferences
{
    public async Task<CourseReferencePage> ExecuteAsync(Guid[] input, CancellationToken cancellationToken)
    {
        if (input.Length is < 1 or > 50 || input.Any(id => id == Guid.Empty)) throw new CourseRuleException("INVALID_REQUEST");
        return new(await queries.ResolveAsync(input, cancellationToken));
    }
}
