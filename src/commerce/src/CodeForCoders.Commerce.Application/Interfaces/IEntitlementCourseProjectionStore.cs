using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IEntitlementCourseProjectionStore
{
    Task<bool> ApplyAsync(PublishedCourseSnapshot snapshot, CancellationToken cancellationToken);
}
