using System.Text.Json;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.CreateOffer;

public sealed record CreateOfferInput(Guid TenantId, Guid ActorId, Guid CourseId, string IdempotencyKey, JsonElement Body);
