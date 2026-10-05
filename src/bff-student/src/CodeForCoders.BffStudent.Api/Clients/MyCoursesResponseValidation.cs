using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public static class MyCoursesResponseValidation
{
    private const int MaximumCourses = 500;
    public static bool IsValid(JsonElement body)
        => body.TryGetProperty("progressAvailable", out var available) && available.ValueKind is JsonValueKind.True or JsonValueKind.False
            && body.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.Array && active.GetArrayLength() <= MaximumCourses
            && body.TryGetProperty("ended", out var ended) && ended.ValueKind == JsonValueKind.Array && ended.GetArrayLength() <= MaximumCourses
            && active.EnumerateArray().All(IsActive) && ended.EnumerateArray().All(IsEnded);

    private static bool IsActive(JsonElement item)
        => IsCourse(item) && item.TryGetProperty("continueLessonId", out var lesson) && IsGuid(lesson)
            && item.TryGetProperty("started", out var started) && started.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null
            && item.TryGetProperty("lastActivityAt", out var activity) && (activity.ValueKind == JsonValueKind.Null
                || activity.ValueKind == JsonValueKind.String && activity.TryGetDateTimeOffset(out _));

    private static bool IsEnded(JsonElement item)
        => IsCourse(item) && item.TryGetProperty("endedOn", out var ended) && ended.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(ended.GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _)
            && item.TryGetProperty("endedReason", out var reason) && reason.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(reason.GetString());

    private static bool IsGuid(JsonElement value)
        => value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) && id != Guid.Empty;

    private static bool IsCourse(JsonElement item)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("courseId", out var course) && IsGuid(course)
            && item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString())
            && item.TryGetProperty("progress", out var progress) && (progress.ValueKind == JsonValueKind.Null || IsProgress(progress));

    private static bool IsProgress(JsonElement body)
        => body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("completedLessons", out var completed) && completed.ValueKind == JsonValueKind.Number && completed.TryGetInt32(out var count) && count >= 0
            && body.TryGetProperty("totalLessons", out var total) && total.ValueKind == JsonValueKind.Number && total.TryGetInt32(out var size) && size >= count
            && body.TryGetProperty("percent", out var percent) && percent.ValueKind == JsonValueKind.Number && percent.TryGetInt32(out var value) && value is >= 0 and <= 100;
}
