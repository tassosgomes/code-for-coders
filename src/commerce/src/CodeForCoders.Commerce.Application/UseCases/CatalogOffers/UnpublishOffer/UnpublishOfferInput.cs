namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;

public sealed record UnpublishOfferInput(Guid TenantId, Guid ActorId, Guid OfferId, string IdempotencyKey, string? TraceParent);
