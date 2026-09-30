using System.Text.Json;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Api.Endpoints;

internal static class CourseChangesRequest
{
    public static CourseChanges Parse(JsonElement body, string[] allowed, bool titleRequired)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.EnumerateObject().Any()
            || body.EnumerateObject().Any(property => !allowed.Contains(property.Name, StringComparer.Ordinal)))
            throw Invalid();
        string? title = null;
        if (body.TryGetProperty("title", out var titleValue))
        {
            if (titleValue.ValueKind != JsonValueKind.String || titleValue.GetString()!.Length > 200) throw Invalid();
            title = titleValue.GetString();
        }
        else if (titleRequired) throw Invalid();
        var hasDescription = body.TryGetProperty("description", out var descriptionValue);
        if (hasDescription && descriptionValue.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw Invalid();
        var description = hasDescription && descriptionValue.ValueKind != JsonValueKind.Null ? descriptionValue.GetString() : null;
        if (description?.Length > 5000) throw Invalid();
        int? position = null;
        if (body.TryGetProperty("position", out var positionValue))
        {
            if (positionValue.ValueKind != JsonValueKind.Number || !positionValue.TryGetInt32(out var value)) throw Invalid();
            position = value;
        }
        Guid? moduleId = null;
        if (body.TryGetProperty("moduleId", out var moduleValue))
        {
            if (moduleValue.ValueKind != JsonValueKind.String || !moduleValue.TryGetGuid(out var value)) throw Invalid();
            moduleId = value;
        }
        var hasVideoId = body.TryGetProperty("videoId", out var videoValue);
        Guid? videoId = null;
        if (hasVideoId && videoValue.ValueKind != JsonValueKind.Null)
        {
            if (videoValue.ValueKind != JsonValueKind.String || !videoValue.TryGetGuid(out var value)) throw Invalid();
            videoId = value;
        }
        var hasLevel = body.TryGetProperty("level", out var levelValue);
        if (hasLevel && levelValue.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw new CourseRuleException("FIELD_INVALID", "level");
        var level = hasLevel && levelValue.ValueKind != JsonValueKind.Null ? levelValue.GetString() : null;
        return new CourseChanges(title, description, hasDescription, position, moduleId, videoId, hasVideoId, level, hasLevel);
    }

    private static ValidationException Invalid() => new("Invalid course changes.");
}
