using System.Text.Json;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ResolveOfferReferences;

public sealed record ResolveOfferReferencesInput(JsonElement Body);
