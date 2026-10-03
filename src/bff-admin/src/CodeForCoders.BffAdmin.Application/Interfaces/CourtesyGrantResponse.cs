using System.Text.Json;
namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourtesyGrantResponse(int StatusCode, string? Code, JsonElement? Record, string? Detail = null);
