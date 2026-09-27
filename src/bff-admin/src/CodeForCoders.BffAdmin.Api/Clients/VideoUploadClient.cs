using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class VideoUploadClient(HttpClient httpClient) : IVideoUploadClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<VideoUploadApiResult<VideoUploadResponse>> CreateAsync(
        CreateVideoUploadInternalRequest request,
        string accessToken,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync<VideoUploadResponse>(
            HttpMethod.Post,
            "internal/v1/video-uploads",
            request,
            accessToken,
            idempotencyKey,
            cancellationToken);

    public Task<VideoUploadApiResult<VideoUploadPageResponse>> ListPendingAsync(
        int page,
        int size,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync<VideoUploadPageResponse>(
            HttpMethod.Get,
            $"internal/v1/video-uploads?_page={page}&_size={size}",
            null,
            accessToken,
            null,
            cancellationToken);

    public Task<VideoUploadApiResult<VideoUploadResponse>> GetAsync(
        Guid uploadId,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync<VideoUploadResponse>(
            HttpMethod.Get,
            $"internal/v1/video-uploads/{uploadId:D}",
            null,
            accessToken,
            null,
            cancellationToken);

    public Task<VideoUploadApiResult<VideoPartUrlsResponse>> CreatePartUrlsAsync(
        Guid uploadId,
        CreateVideoUploadPartUrlsRequest request,
        string accessToken,
        CancellationToken cancellationToken)
        => SendAsync<VideoPartUrlsResponse>(
            HttpMethod.Post,
            $"internal/v1/video-uploads/{uploadId:D}/part-urls",
            request,
            accessToken,
            null,
            cancellationToken);

    public Task<VideoUploadApiResult<VideoResponse>> CompleteAsync(
        Guid uploadId,
        string accessToken,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync<VideoResponse>(
            HttpMethod.Post,
            $"internal/v1/video-uploads/{uploadId:D}/complete",
            null,
            accessToken,
            idempotencyKey,
            cancellationToken);

    private async Task<VideoUploadApiResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        string accessToken,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode < 300)
            {
                try
                {
                    var content = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
                    return content is null
                        ? Unavailable<TResponse>(HttpStatusCode.BadGateway)
                        : new VideoUploadApiResult<TResponse>(response.StatusCode, null, content);
                }
                catch (JsonException)
                {
                    return Unavailable<TResponse>(HttpStatusCode.BadGateway);
                }
            }

            var code = await ReadProblemCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.GatewayTimeout)
            {
                return Unavailable<TResponse>(HttpStatusCode.GatewayTimeout);
            }

            if ((int)response.StatusCode >= 500)
            {
                return Unavailable<TResponse>(HttpStatusCode.BadGateway);
            }

            return new VideoUploadApiResult<TResponse>(response.StatusCode, code, default);
        }
        catch (TimeoutRejectedException)
        {
            return Unavailable<TResponse>(HttpStatusCode.GatewayTimeout);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable<TResponse>(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException)
        {
            return Unavailable<TResponse>(HttpStatusCode.BadGateway);
        }
    }

    private static async Task<string?> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static VideoUploadApiResult<TResponse> Unavailable<TResponse>(HttpStatusCode statusCode)
        => new(statusCode, "MEDIA_UNAVAILABLE", default);
}
