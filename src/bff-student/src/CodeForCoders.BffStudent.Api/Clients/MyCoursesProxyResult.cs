using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record MyCoursesProxyResult(int StatusCode, string? Code = null, JsonElement? Body = null);
