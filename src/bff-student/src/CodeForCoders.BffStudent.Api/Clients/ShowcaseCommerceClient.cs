using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class ShowcaseCommerceClient(HttpClient httpClient) : IShowcaseCommerceClient
{
    public const string CourseNotFoundCode = "SHOWCASE_COURSE_NOT_FOUND";

    private const string ReadScope = "showcase:read";

    public Task<ShowcaseResult<ShowcaseCoursePageV1>> ListCoursesAsync(
        string? level,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (level is not null)
        {
            query.Add($"level={Uri.EscapeDataString(level)}");
        }

        query.Add($"_page={page}");
        query.Add($"_size={size}");
        return ReadAsync<ShowcaseCoursePageV1>($"internal/v1/showcase/courses?{string.Join('&', query)}", cancellationToken);
    }

    public Task<ShowcaseResult<ShowcaseCourseDetailV1>> GetCourseAsync(Guid courseId, CancellationToken cancellationToken)
        => ReadAsync<ShowcaseCourseDetailV1>($"internal/v1/showcase/courses/{courseId:D}", cancellationToken);

    private async Task<ShowcaseResult<T>> ReadAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Options.Set(ServiceAssertionHandler.ScopeKey, ReadScope);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return result is null
                    ? Unavailable<T>()
                    : new ShowcaseResult<T>(StatusCodes.Status200OK, null, result);
            }

            var code = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound
                ? await ReadCodeAsync(response, cancellationToken)
                : null;
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new ShowcaseResult<T>(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
            }

            return response.StatusCode == HttpStatusCode.NotFound && code == CourseNotFoundCode
                ? new ShowcaseResult<T>(StatusCodes.Status404NotFound, CourseNotFoundCode)
                : Unavailable<T>();
        }
        catch (HttpRequestException)
        {
            return Unavailable<T>();
        }
        catch (JsonException)
        {
            return Unavailable<T>();
        }
        catch (TimeoutRejectedException)
        {
            return Timeout<T>();
        }
        catch (ExecutionRejectedException)
        {
            return Unavailable<T>();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Timeout<T>();
        }
    }

    private static ShowcaseResult<T> Unavailable<T>()
        where T : class
        => new(StatusCodes.Status502BadGateway, "SHOWCASE_UNAVAILABLE");

    private static ShowcaseResult<T> Timeout<T>()
        where T : class
        => new(StatusCodes.Status504GatewayTimeout, "SHOWCASE_TIMEOUT");

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("code", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
