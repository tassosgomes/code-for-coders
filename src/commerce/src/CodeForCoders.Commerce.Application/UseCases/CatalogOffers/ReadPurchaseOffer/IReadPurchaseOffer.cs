using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ReadPurchaseOffer;

public interface IReadPurchaseOffer : IUseCase<Guid, PurchaseOffer?>;
