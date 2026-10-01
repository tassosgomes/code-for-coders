using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.ListCatalogCourses;

public sealed class ListCatalogCourses(ICatalogCourseQueries queries, IValidator<ListCatalogCoursesInput> validator)
    : IListCatalogCourses
{
    public async Task<CatalogCoursePage> ExecuteAsync(ListCatalogCoursesInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return await queries.ListAsync(input.Page, input.Size, cancellationToken);
    }
}
