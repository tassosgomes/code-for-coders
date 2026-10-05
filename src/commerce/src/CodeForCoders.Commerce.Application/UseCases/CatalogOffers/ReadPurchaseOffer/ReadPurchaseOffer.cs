using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ReadPurchaseOffer;

public sealed class ReadPurchaseOffer(ICatalogPurchaseOfferQueries queries) : IReadPurchaseOffer, IPurchaseOfferReader
{
    public Task<PurchaseOffer?> ExecuteAsync(Guid input, CancellationToken cancellationToken) => queries.FindAsync(input, cancellationToken);
    public Task<PurchaseOffer?> FindAsync(Guid offerId, CancellationToken cancellationToken) => ExecuteAsync(offerId, cancellationToken);
}
