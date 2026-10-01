using System.Text.Json;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UpdateOffer;

public sealed record UpdateOfferInput(Guid TenantId, Guid ActorId, Guid OfferId, string IdempotencyKey, JsonElement Body);
