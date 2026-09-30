using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class CourseAuthoringClient(HttpClient httpClient) : ICourseAuthoringClient
{
    public async Task<CourseClientResult> SendAsync(CourseClientRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(input.Method == "GET" && input.Body is not null ? "POST" : input.Method), input.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        if (request.Method != HttpMethod.Get)
        {
            request.Headers.Add("X-Actor-Name", input.ActorName);
            request.Headers.Add("Idempotency-Key", input.IdempotencyKey);
            if (input.Body is not null) request.Content = JsonContent.Create(input.Body);
        }
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return new(204, null, null, null, null);
                if (input.Path == "internal/v1/course-references/resolve")
                {
                    var references = await response.Content.ReadFromJsonAsync<CourseReferencePage>(cancellationToken);
                    return references is null ? Unavailable(502) : new((int)response.StatusCode, null, null, null, null, References: references);
                }
                if (input.Path.Contains("/versions?", StringComparison.Ordinal))
                {
                    var versions = await response.Content.ReadFromJsonAsync<CourseVersionSummaryPage>(cancellationToken);
                    return versions is null ? Unavailable(502) : new((int)response.StatusCode, null, null, null, null, Versions: versions);
                }
                if (input.Path.Contains("/versions/", StringComparison.Ordinal) || (input.Path.EndsWith("/versions", StringComparison.Ordinal) && input.Method == "POST"))
                {
                    var version = await response.Content.ReadFromJsonAsync<CourseVersion>(cancellationToken);
                    return version is null ? Unavailable(502) : new((int)response.StatusCode, null, null, null, null, response.Headers.Location?.OriginalString, version);
                }
                if (input.Path.Contains('?', StringComparison.Ordinal))
                {
                    var page = await response.Content.ReadFromJsonAsync<CoursePage>(cancellationToken);
                    return page is null ? Unavailable(502) : new((int)response.StatusCode, page, null, null, null);
                }
                var course = await response.Content.ReadFromJsonAsync<CourseDetail>(cancellationToken);
                return course is null ? Unavailable(502) : new((int)response.StatusCode, null, CourseDraftMapper.ToPublic(course), null, null, response.Headers.Location?.OriginalString);
            }
            if ((int)response.StatusCode >= 500) return Unavailable(response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ? 504 : 502);
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = problem.RootElement;
            return new((int)response.StatusCode, null, null,
                root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST",
                root.TryGetProperty("errors", out var errors) ? errors.Clone() : null,
                Pendencies: root.TryGetProperty("pendencies", out var pendencies) ? pendencies.Clone() : null);
        }
        catch (JsonException) { return Unavailable(502); }
        catch (HttpRequestException) { return Unavailable(502); }
        catch (TimeoutRejectedException) { return Unavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(504); }
    }

    private static CourseClientResult Unavailable(int status) => new(status, null, null, "LEARNING_UNAVAILABLE", null);
}
