using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogCourseEditStore
{
    Task<ICatalogEditTransaction> LockAsync(CatalogEditScope scope, CancellationToken cancellationToken);
    Task<CatalogCourseView?> GetAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CatalogEditReceipt?> FindAsync(CatalogEditScope scope, CancellationToken cancellationToken);
    Task<Guid?> FindOfferCourseAsync(Guid offerId, CancellationToken cancellationToken);
    void Add(CatalogEditReceipt receipt);
}
