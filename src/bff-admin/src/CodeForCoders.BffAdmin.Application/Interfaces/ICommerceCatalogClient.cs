namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICommerceCatalogClient
{
    Task<CatalogCourseRecordResult> CreateOfferAsync(CatalogOfferRequest request, CancellationToken cancellationToken);
    Task<CatalogCourseRecordResult> UpdateOfferAsync(CatalogOfferRequest request, CancellationToken cancellationToken);
    Task<CatalogCourseRecordResult> DeleteOfferAsync(CatalogOfferRequest request, CancellationToken cancellationToken);
    Task<CatalogCourseRecordResult> GetAsync(CatalogCourseRecordRequest request, CancellationToken cancellationToken);
    Task<CatalogCourseRecordResult> UpdateAsync(CatalogCourseRecordRequest request, CancellationToken cancellationToken);
    Task<CatalogCoursesResult> ListAsync(CatalogCoursesRequest request, CancellationToken cancellationToken);
}
