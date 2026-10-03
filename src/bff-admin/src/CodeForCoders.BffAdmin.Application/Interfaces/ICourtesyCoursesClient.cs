namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface ICourtesyCoursesClient
{
    Task<CatalogCoursesResult> ListAsync(CourtesyCoursesRequest input, CancellationToken cancellationToken);
}
