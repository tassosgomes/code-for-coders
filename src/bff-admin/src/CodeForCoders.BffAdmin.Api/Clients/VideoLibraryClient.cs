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
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos?_page={page}&_size={size}", accessToken, true, cancellationToken);

    public Task<VideoLibraryResult> GetVideoAsync(
        Guid videoId,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}", accessToken, false, cancellationToken);

    private async Task<VideoLibraryResult> SendAsync(
        string path,
        string accessToken,
        bool isList,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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

        if (response.StatusCode == HttpStatusCode.NotFound)
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
