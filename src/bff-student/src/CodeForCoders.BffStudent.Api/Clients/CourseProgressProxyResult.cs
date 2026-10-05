using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record CourseProgressProxyResult(int StatusCode, string? Code = null, JsonElement? Body = null,
    string? Reason = null, DateTimeOffset? AccessEndedAt = null);
