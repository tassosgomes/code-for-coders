using System.Text.Json;
using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Infra.Messaging;

public static class PublishedCourseFact
{
    public const string RoutingKey = "conteudo.versao-publicada.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PublishedCourseSnapshot Parse(ReadOnlyMemory<byte> body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var tenantId = ReadId(root, "tenantId");
        var courseId = ReadId(root, "courseId");
        _ = ReadId(root, "eventId");
        _ = ReadId(root, "publishedById");
        var version = Field(root, "versionNumber");
        if (!version.TryGetInt32(out var versionNumber) || versionNumber < 1) throw Invalid();
        if (!Field(root, "publishedAt").TryGetDateTimeOffset(out var publishedAt)) throw Invalid();
        var title = ReadText(root, "title", 200);
        var hasDescription = root.TryGetProperty("description", out var description);
        var hasLevel = root.TryGetProperty("level", out var level);
        var hasPrerequisite = root.TryGetProperty("prerequisite", out var prerequisite);
        var rich = hasDescription && hasLevel && hasPrerequisite;
        if ((hasDescription || hasLevel || hasPrerequisite) && !rich) throw Invalid();
        var levelText = rich ? NullableText(level, 20) : null;
        if (levelText is not (null or "beginner" or "intermediate" or "advanced")) throw Invalid();
        var prerequisiteJson = rich && prerequisite.ValueKind != JsonValueKind.Null
            ? ReadPrerequisite(prerequisite) : "{\"text\":null,\"recommendedCourses\":[]}";
        return new PublishedCourseSnapshot(tenantId, courseId, versionNumber, rich ? "1.1.0" : "1.0.0",
            title, rich ? NullableText(description, 5000) ?? "" : "", levelText,
            prerequisiteJson, ReadStructure(Field(root, "modules")), publishedAt.ToUniversalTime());
    }

    private static string ReadPrerequisite(JsonElement prerequisite)
    {
        _ = NullableText(Field(prerequisite, "text"), 1000);
        var courses = Field(prerequisite, "recommendedCourses");
        if (courses.ValueKind != JsonValueKind.Array || courses.GetArrayLength() > 5) throw Invalid();
        var recommendations = courses.EnumerateArray().Select(course => new
        {
            courseId = ReadId(course, "courseId"),
            title = ReadText(course, "title", 200),
        }).ToArray();
        return JsonSerializer.Serialize(new { text = NullableText(Field(prerequisite, "text"), 1000), recommendedCourses = recommendations }, JsonOptions);
    }

    private static string ReadStructure(JsonElement modules)
    {
        if (modules.ValueKind != JsonValueKind.Array || modules.GetArrayLength() is < 1 or > 100) throw Invalid();
        var structure = modules.EnumerateArray().Select(module =>
        {
            var lessons = Field(module, "lessons");
            if (lessons.ValueKind != JsonValueKind.Array || lessons.GetArrayLength() is < 1 or > 200) throw Invalid();
            return new
            {
                moduleId = ReadId(module, "moduleId"),
                title = ReadText(module, "title", 200),
                position = ReadPosition(module),
                lessons = lessons.EnumerateArray().Select(lesson => new
                {
                    lessonId = ReadId(lesson, "lessonId"),
                    title = ReadText(lesson, "title", 200),
                    position = ReadPosition(lesson),
                }).ToArray(),
            };
        }).ToArray();
        return JsonSerializer.Serialize(structure, JsonOptions);
    }

    private static int ReadPosition(JsonElement value)
    {
        if (!Field(value, "position").TryGetInt32(out var position) || position < 1) throw Invalid();
        return position;
    }

    private static JsonElement Field(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) ? field : throw Invalid();

    private static Guid ReadId(JsonElement value, string name)
    {
        var field = Field(value, name);
        if (field.ValueKind != JsonValueKind.String || !field.TryGetGuid(out var id) || id == Guid.Empty) throw Invalid();
        return id;
    }

    private static string ReadText(JsonElement value, string name, int maximum)
    {
        var text = NullableText(Field(value, name), maximum);
        return !string.IsNullOrWhiteSpace(text) ? text : throw Invalid();
    }

    private static string? NullableText(JsonElement value, int maximum)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > maximum) throw Invalid();
        return value.GetString();
    }

    private static JsonException Invalid() => new("The published course fact is invalid.");
}
