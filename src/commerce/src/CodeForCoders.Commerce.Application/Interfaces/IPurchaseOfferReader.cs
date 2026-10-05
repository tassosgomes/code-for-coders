namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IPurchaseOfferReader
{
    Task<PurchaseOffer?> FindAsync(Guid offerId, CancellationToken cancellationToken);
}
