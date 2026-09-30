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
        using var request = new HttpRequestMessage(input.Body is null ? HttpMethod.Get : HttpMethod.Post, input.Path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", input.AccessToken);
        if (input.Body is not null)
        {
            request.Headers.Add("X-Actor-Name", input.ActorName);
            request.Headers.Add("Idempotency-Key", input.IdempotencyKey);
            request.Content = JsonContent.Create(input.Body);
        }
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                if (input.Path.Contains('?', StringComparison.Ordinal))
                {
                    var page = await response.Content.ReadFromJsonAsync<CoursePage>(cancellationToken);
                    return page is null ? Unavailable(502) : new((int)response.StatusCode, page, null, null, null);
                }
                var course = await response.Content.ReadFromJsonAsync<CourseDetail>(cancellationToken);
                return course is null ? Unavailable(502) : new((int)response.StatusCode, null, course, null, null);
            }
            if ((int)response.StatusCode >= 500) return Unavailable(response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ? 504 : 502);
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = problem.RootElement;
            return new((int)response.StatusCode, null, null,
                root.TryGetProperty("code", out var code) ? code.GetString() : "INVALID_REQUEST",
                root.TryGetProperty("errors", out var errors) ? errors.Clone() : null);
        }
        catch (JsonException) { return Unavailable(502); }
        catch (HttpRequestException) { return Unavailable(502); }
        catch (TimeoutRejectedException) { return Unavailable(504); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return Unavailable(504); }
    }

    private static CourseClientResult Unavailable(int status) => new(status, null, null, "LEARNING_UNAVAILABLE", null);
}
