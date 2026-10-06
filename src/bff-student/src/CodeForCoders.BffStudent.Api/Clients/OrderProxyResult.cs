using System.Text.Json;
namespace CodeForCoders.BffStudent.Api.Clients;

public sealed record OrderProxyResult(int StatusCode, string? Code = null, JsonElement? Body = null);
