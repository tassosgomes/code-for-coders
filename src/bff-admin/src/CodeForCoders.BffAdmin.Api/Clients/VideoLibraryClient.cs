using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class VideoLibraryClient(HttpClient httpClient) : IVideoLibraryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<VideoLibraryResult> ListVideosAsync(
        int page,
        int size,
        IReadOnlyList<string> statuses,
        string? query,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var filters = string.Concat(statuses.Select(status => $"&status={Uri.EscapeDataString(status)}"));
        if (query is not null) filters += $"&q={Uri.EscapeDataString(query)}";
        return SendAsync($"internal/v1/videos?_page={page}&_size={size}{filters}", accessToken, true, cancellationToken);
    }

    public Task<VideoLibraryResult> GetVideoAsync(
        Guid videoId,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}", accessToken, false, cancellationToken);

    public Task<VideoLibraryResult> UpdateVideoTitleAsync(
        Guid videoId,
        string title,
        string idempotencyKey,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}", accessToken, false, cancellationToken, HttpMethod.Patch, title, idempotencyKey);

    private async Task<VideoLibraryResult> SendAsync(
        string path,
        string accessToken,
        bool isList,
        CancellationToken cancellationToken,
        HttpMethod? method = null,
        string? title = null,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new UpdateVideoTitleRequest(title!));
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ReadResponseAsync(response, isList, cancellationToken);
        }
        catch (TimeoutRejectedException)
        {
            return Unavailable(HttpStatusCode.GatewayTimeout);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return Unavailable(HttpStatusCode.BadGateway);
        }
    }

    private static async Task<VideoLibraryResult> ReadResponseAsync(
        HttpResponseMessage response,
        bool isList,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            try
            {
                if (isList)
                {
                    var page = await response.Content.ReadFromJsonAsync<VideoPageResponse>(JsonOptions, cancellationToken);
                    return page is null
                        ? Unavailable(HttpStatusCode.BadGateway)
                        : new VideoLibraryResult(response.StatusCode, null, page, null);
                }

                var video = await response.Content.ReadFromJsonAsync<VideoResponse>(JsonOptions, cancellationToken);
                return video is null
                    ? Unavailable(HttpStatusCode.BadGateway)
                    : new VideoLibraryResult(response.StatusCode, null, null, video);
            }
            catch (JsonException)
            {
                return Unavailable(HttpStatusCode.BadGateway);
            }
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest)
        {
            return new VideoLibraryResult(
                response.StatusCode,
                await ReadProblemCodeAsync(response, cancellationToken),
                null,
                null);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new VideoLibraryResult(
                response.StatusCode,
                await ReadProblemCodeAsync(response, cancellationToken),
                null,
                null);
        }

        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return Unavailable(HttpStatusCode.GatewayTimeout);
        }

        if ((int)response.StatusCode >= 500)
        {
            return Unavailable(HttpStatusCode.BadGateway);
        }

        return new VideoLibraryResult(
            response.StatusCode,
            await ReadProblemCodeAsync(response, cancellationToken),
            null,
            null);
    }

    private static async Task<string?> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return problem.RootElement.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static VideoLibraryResult Unavailable(HttpStatusCode statusCode)
        => new(statusCode, "MEDIA_UNAVAILABLE", null, null);
}
