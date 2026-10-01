namespace CodeForCoders.BffStudent.Api.Clients;

public interface IShowcaseCommerceClient
{
    Task<ShowcaseListResult> ListCoursesAsync(string? level, int page, int size, CancellationToken cancellationToken);
}
