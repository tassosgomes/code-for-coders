namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.DeleteOffer;

public sealed record DeleteOfferInput(Guid TenantId, Guid ActorId, Guid OfferId, string IdempotencyKey);
