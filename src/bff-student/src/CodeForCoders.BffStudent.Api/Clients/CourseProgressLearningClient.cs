using System.Net;
using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class CourseProgressLearningClient(HttpClient client) : ICourseProgressLearningClient
{
    public async Task<CourseProgressProxyResult> GetAsync(Guid courseId, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/student-courses/{courseId:D}/progress");
        request.Headers.Authorization = new("Bearer", accessToken);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object) return new(502, "UPSTREAM_UNAVAILABLE");
            if (response.StatusCode == HttpStatusCode.OK && IsProgress(body, courseId))
                return new(200, Body: body.Clone());
            var code = body.TryGetProperty("code", out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
            if (response.StatusCode == HttpStatusCode.Forbidden && code == "ACCESS_DENIED")
                return new(403, code, Reason: body.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null,
                    AccessEndedAt: body.TryGetProperty("accessEndedAt", out var ended) && ended.ValueKind == JsonValueKind.String && ended.TryGetDateTimeOffset(out var value) ? value : null);
            if (response.StatusCode == HttpStatusCode.NotFound && code == "COURSE_NOT_AVAILABLE") return new(404, code);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && code == "ACCESS_DECISION_UNAVAILABLE") return new(503, code);
            return new(502, "UPSTREAM_UNAVAILABLE");
        }
        catch (HttpRequestException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (JsonException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT"); }
    }

    private static bool IsProgress(JsonElement body, Guid courseId)
        => body.TryGetProperty("courseId", out var course) && course.ValueKind == JsonValueKind.String && course.TryGetGuid(out var id) && id == courseId
            && body.TryGetProperty("versionNumber", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number) && number > 0
            && body.TryGetProperty("completedLessons", out var completed) && completed.ValueKind == JsonValueKind.Number && completed.TryGetInt32(out var count) && count >= 0
            && body.TryGetProperty("totalLessons", out var total) && total.ValueKind == JsonValueKind.Number && total.TryGetInt32(out var size) && size >= count
            && body.TryGetProperty("percent", out var percent) && percent.ValueKind == JsonValueKind.Number && percent.TryGetInt32(out var value) && value is >= 0 and <= 100
            && body.TryGetProperty("lessons", out var lessons) && lessons.ValueKind == JsonValueKind.Array;
}
