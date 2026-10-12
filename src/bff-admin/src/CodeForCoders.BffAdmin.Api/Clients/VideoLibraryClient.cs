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
        VideoLibraryListRequest filters,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = string.Concat(filters.Statuses.Select(status => $"&status={Uri.EscapeDataString(status)}"));
        if (filters.Query is not null) query += $"&q={Uri.EscapeDataString(filters.Query)}";
        query += string.Concat((filters.Tags ?? []).Select(tag => $"&tag={Uri.EscapeDataString(tag)}"));
        if (filters.Tags?.Count > 0 || filters.TagMatch != "all")
            query += $"&tagMatch={Uri.EscapeDataString(filters.TagMatch)}";
        return SendAsync($"internal/v1/videos?_page={filters.Page}&_size={filters.Size}{query}", accessToken, VideoLibraryResponseKind.Page, cancellationToken);
    }

    public Task<VideoLibraryResult> ListVideoTagsAsync(string? query, string accessToken, CancellationToken cancellationToken)
        => SendAsync("internal/v1/video-tags" + (query is null ? string.Empty : $"?q={Uri.EscapeDataString(query)}"),
            accessToken, VideoLibraryResponseKind.Tags, cancellationToken);

    public Task<VideoLibraryResult> GetVideoAsync(
        Guid videoId,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}", accessToken, VideoLibraryResponseKind.Video, cancellationToken);

    public Task<VideoLibraryResult> UpdateVideoTitleAsync(
        Guid videoId,
        string title,
        string idempotencyKey,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}", accessToken, VideoLibraryResponseKind.Video, cancellationToken, HttpMethod.Patch, new UpdateVideoTitleRequest(title), idempotencyKey);

    public Task<VideoLibraryResult> ReplaceVideoTagsAsync(
        Guid videoId, IReadOnlyList<string> tags, string idempotencyKey, string accessToken, CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}/tags", accessToken, VideoLibraryResponseKind.Video, cancellationToken,
            HttpMethod.Put, new ReplaceVideoTagsRequest(tags), idempotencyKey);

    public Task<VideoLibraryResult> RequestVideoTranscriptionAsync(
        Guid videoId, string idempotencyKey, string accessToken, CancellationToken cancellationToken)
        => SendAsync($"internal/v1/videos/{videoId:D}/transcription-requests", accessToken, VideoLibraryResponseKind.Video,
            cancellationToken, HttpMethod.Post, idempotencyKey: idempotencyKey);

    private async Task<VideoLibraryResult> SendAsync(
        string path,
        string accessToken,
        VideoLibraryResponseKind kind,
        CancellationToken cancellationToken,
        HttpMethod? method = null,
        object? body = null,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            if (body is not null) request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ReadResponseAsync(response, kind, cancellationToken);
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
        catch (Polly.ExecutionRejectedException)
        {
            return Unavailable(HttpStatusCode.BadGateway);
        }
    }

    private static async Task<VideoLibraryResult> ReadResponseAsync(
        HttpResponseMessage response,
        VideoLibraryResponseKind kind,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
        {
            try
            {
                if (kind == VideoLibraryResponseKind.Page)
                {
                    var page = await response.Content.ReadFromJsonAsync<VideoPageResponse>(JsonOptions, cancellationToken);
                    return page is null
                        ? Unavailable(HttpStatusCode.BadGateway)
                        : new VideoLibraryResult(response.StatusCode, null, page, null);
                }

                if (kind == VideoLibraryResponseKind.Tags)
                {
                    var tags = await response.Content.ReadFromJsonAsync<VideoTagListResponse>(JsonOptions, cancellationToken);
                    return tags?.Data is null ? Unavailable(HttpStatusCode.BadGateway)
                        : new VideoLibraryResult(response.StatusCode, null, null, null, tags);
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
