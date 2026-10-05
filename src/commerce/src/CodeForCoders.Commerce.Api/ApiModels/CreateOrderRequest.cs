using System.Text.Json.Serialization;
namespace CodeForCoders.Commerce.Api.ApiModels;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest(Guid OfferId);
