namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogCourseQueries
{
    Task<CatalogCoursePage> ListAsync(int page, int size, CancellationToken cancellationToken);
}
