using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.GetCatalogCourse;

public sealed class GetCatalogCourse(ICatalogCourseQueries queries) : IGetCatalogCourse
{
    public async Task<CatalogCourseDetail> ExecuteAsync(Guid input, CancellationToken cancellationToken)
        => await queries.GetAsync(input, cancellationToken) ?? throw new NotFoundException("CATALOG_COURSE_NOT_FOUND");
}
