using System.Text.Json.Serialization;

namespace CodeForCoders.Commerce.Api.ApiModels;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CourtesyGrantRequest(Guid StudentId, Guid CourseId, AccessPeriodRequest? AccessPeriod, string? Reason);
