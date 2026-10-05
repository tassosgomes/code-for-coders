using System.Text.Json.Serialization;
namespace CodeForCoders.BffStudent.Api.ApiModels;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest(Guid OfferId);
