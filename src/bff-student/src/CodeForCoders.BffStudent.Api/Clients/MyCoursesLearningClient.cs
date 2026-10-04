using System.Net;
using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class MyCoursesLearningClient(HttpClient client) : IMyCoursesLearningClient
{
    public async Task<MyCoursesProxyResult> ListAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/v1/student-courses");
        request.Headers.Authorization = new("Bearer", accessToken);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object) return new(502, "UPSTREAM_UNAVAILABLE");
            if (response.StatusCode == HttpStatusCode.OK && MyCoursesResponseValidation.IsValid(body))
                return new(200, Body: body.Clone());
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable
                && body.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                && code.GetString() == "COURSE_ACCESS_UNAVAILABLE") return new(503, "COURSE_ACCESS_UNAVAILABLE");
            return new(502, "UPSTREAM_UNAVAILABLE");
        }
        catch (HttpRequestException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (JsonException) { return new(502, "UPSTREAM_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT"); }
    }
}
