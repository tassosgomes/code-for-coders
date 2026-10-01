using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;

public interface IUnpublishOffer : IUseCase<UnpublishOfferInput, CatalogOfferDetail>;
