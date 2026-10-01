using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.CreateOffer;

public interface ICreateOffer : IUseCase<CreateOfferInput, CatalogOfferDetail>;
