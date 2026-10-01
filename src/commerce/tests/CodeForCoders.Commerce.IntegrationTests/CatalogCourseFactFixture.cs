using System.Text.Json;

namespace CodeForCoders.Commerce.IntegrationTests;

public static class CatalogCourseFactFixture
{
    public static byte[] Create(Guid tenantId, Guid courseId, int version = 1, bool rich = false, string title = "Published course", string? level = "beginner", string description = "Pedagogical description", object? prerequisite = null, object[]? modules = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["eventId"] = courseId,
            ["tenantId"] = tenantId,
            ["courseId"] = courseId,
            ["versionNumber"] = version,
            ["publishedAt"] = DateTimeOffset.UtcNow,
            ["publishedById"] = Guid.CreateVersion7(),
            ["title"] = title,
            ["modules"] = modules ?? new[] { new { moduleId = Guid.CreateVersion7(), title = "Module", position = 1,
                lessons = new[] { new { lessonId = Guid.CreateVersion7(), title = "Lesson", position = 1, videoId = Guid.CreateVersion7() } } } },
        };
        if (rich)
        {
            fields["description"] = description; fields["level"] = level;
            fields["prerequisite"] = prerequisite ?? new { text = (string?)null, recommendedCourses = Array.Empty<object>() };
        }
        return JsonSerializer.SerializeToUtf8Bytes(fields);
    }
}
