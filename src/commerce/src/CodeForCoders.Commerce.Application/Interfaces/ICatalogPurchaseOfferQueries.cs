namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogPurchaseOfferQueries
{
    Task<PurchaseOffer?> FindAsync(Guid offerId, CancellationToken cancellationToken);
}
