using System.Text.Json;

namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CatalogCourseRecordRequest(string AccessToken, Guid CourseId, JsonElement? Body = null, string? IdempotencyKey = null);
