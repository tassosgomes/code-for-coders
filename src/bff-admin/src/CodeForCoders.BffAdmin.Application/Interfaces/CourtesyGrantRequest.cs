using System.Text.Json;
namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourtesyGrantRequest(string AccessToken, string Path, JsonElement? Body, string? IdempotencyKey);
