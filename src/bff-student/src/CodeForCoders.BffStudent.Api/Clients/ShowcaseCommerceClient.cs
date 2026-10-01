using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class ShowcaseCommerceClient(HttpClient httpClient) : IShowcaseCommerceClient
{
    private const string ReadScope = "showcase:read";

    public async Task<ShowcaseListResult> ListCoursesAsync(
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
        using var message = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/showcase/courses?{string.Join('&', query)}");
        message.Options.Set(ServiceAssertionHandler.ScopeKey, ReadScope);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<ShowcaseCoursePageV1>(cancellationToken);
                return result is null
                    ? Unavailable()
                    : new ShowcaseListResult(StatusCodes.Status200OK, null, result);
            }

            return response.StatusCode == HttpStatusCode.BadRequest && await ReadCodeAsync(response, cancellationToken) == "INVALID_REQUEST"
                ? new ShowcaseListResult(StatusCodes.Status400BadRequest, "INVALID_REQUEST")
                : Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }
        catch (JsonException)
        {
            return Unavailable();
        }
        catch (TimeoutRejectedException)
        {
            return Timeout();
        }
        catch (ExecutionRejectedException)
        {
            return Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Timeout();
        }
    }

    private static ShowcaseListResult Unavailable()
        => new(StatusCodes.Status502BadGateway, "SHOWCASE_UNAVAILABLE");

    private static ShowcaseListResult Timeout()
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
