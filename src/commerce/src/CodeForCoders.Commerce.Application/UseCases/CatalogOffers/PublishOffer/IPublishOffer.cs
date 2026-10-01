using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.PublishOffer;

public interface IPublishOffer : IUseCase<PublishOfferInput, CatalogOfferDetail>;
