using System.Net;
using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class StudentLessonLearningClient(HttpClient client) : IStudentLessonLearningClient
{
    public async Task<StudentLessonProxyResult> GetAsync(Guid lessonId, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/lessons/{lessonId:D}");
        request.Headers.Authorization = new("Bearer", accessToken);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object) return new(502, "UPSTREAM_UNAVAILABLE");
            if (response.StatusCode == HttpStatusCode.OK && body.TryGetProperty("lesson", out var lesson) && lesson.ValueKind == JsonValueKind.Object
                && body.TryGetProperty("course", out var course) && course.ValueKind == JsonValueKind.Object)
                return new(200, Body: body.Clone());
            var code = body.TryGetProperty("code", out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
            if (response.StatusCode == HttpStatusCode.Forbidden && code == "ACCESS_DENIED")
                return new(403, code, Reason: body.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null,
                    AccessEndedAt: body.TryGetProperty("accessEndedAt", out var ended) && ended.ValueKind == JsonValueKind.String && ended.TryGetDateTimeOffset(out var value) ? value : null);
            if (response.StatusCode == HttpStatusCode.NotFound && code == "LESSON_NOT_AVAILABLE") return new(404, code);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && code == "ACCESS_DECISION_UNAVAILABLE") return new(503, code);
            return new(502, "UPSTREAM_UNAVAILABLE");
        }
        catch (HttpRequestException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (JsonException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT"); }
    }
}
