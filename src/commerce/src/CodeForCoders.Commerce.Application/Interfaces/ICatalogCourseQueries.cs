namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogCourseQueries
{
    Task<CatalogCourseDetail?> GetAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CatalogCoursePage> ListAsync(int page, int size, CancellationToken cancellationToken);
}
