namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.PublishOffer;

public sealed record PublishOfferInput(Guid TenantId, Guid ActorId, Guid OfferId, string IdempotencyKey, string? TraceParent);
