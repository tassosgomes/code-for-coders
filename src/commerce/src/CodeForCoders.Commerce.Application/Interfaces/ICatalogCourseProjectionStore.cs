using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogCourseProjectionStore
{
    Task<bool> ApplyAsync(PublishedCourseSnapshot snapshot, CancellationToken cancellationToken);
}
