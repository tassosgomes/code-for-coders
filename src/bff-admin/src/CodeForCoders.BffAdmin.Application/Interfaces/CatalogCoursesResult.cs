using System.Text.Json;

namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CatalogCoursesResult(int StatusCode, string? Code, JsonElement? Page);
