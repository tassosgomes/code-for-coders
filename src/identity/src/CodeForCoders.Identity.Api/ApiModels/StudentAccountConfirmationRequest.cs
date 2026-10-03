using System.Text.Json.Serialization;
namespace CodeForCoders.Identity.Api.ApiModels;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudentAccountConfirmationRequest(Guid StudentId);
