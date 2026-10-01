using System.Text.Json;

namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CatalogOfferRequest(string AccessToken, Guid TargetId, JsonElement? Body, string IdempotencyKey);
