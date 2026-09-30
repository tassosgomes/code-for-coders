namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICommerceCatalogClient
{
    Task<CatalogCoursesResult> ListAsync(CatalogCoursesRequest request, CancellationToken cancellationToken);
}
