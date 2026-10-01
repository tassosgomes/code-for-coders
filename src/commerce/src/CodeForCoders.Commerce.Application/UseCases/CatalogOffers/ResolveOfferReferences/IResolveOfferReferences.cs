using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ResolveOfferReferences;

public interface IResolveOfferReferences : IUseCase<ResolveOfferReferencesInput, OfferReferenceList>;
