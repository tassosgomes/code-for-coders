using System.Text.Json.Serialization;

namespace CodeForCoders.Learning.Api.Endpoints;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourseRequest(string Title, string? Description);
