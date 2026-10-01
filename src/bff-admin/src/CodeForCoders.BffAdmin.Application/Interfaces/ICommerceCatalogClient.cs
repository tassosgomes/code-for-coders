namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICommerceCatalogClient
{
    Task<CatalogCourseRecordResult> GetAsync(CatalogCourseRecordRequest request, CancellationToken cancellationToken);
    Task<CatalogCourseRecordResult> UpdateAsync(CatalogCourseRecordRequest request, CancellationToken cancellationToken);
    Task<CatalogCoursesResult> ListAsync(CatalogCoursesRequest request, CancellationToken cancellationToken);
}
