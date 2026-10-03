using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyCourses.ListCourtesyCourses;

public sealed class ListCourtesyCourses(ICourtesyCourseQueries queries, IValidator<ListCourtesyCoursesInput> validator)
    : IListCourtesyCourses
{
    public async Task<CourtesyCoursePage> ExecuteAsync(ListCourtesyCoursesInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return await queries.ListAsync(new(input.Page, input.Size, input.Title), cancellationToken);
    }
}
