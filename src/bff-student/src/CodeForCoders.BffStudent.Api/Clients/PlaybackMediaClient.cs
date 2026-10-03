using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class PlaybackMediaClient(HttpClient client) : IPlaybackMediaClient
{
    private static readonly HashSet<(int, string)> KnownErrors =
    [
        (400, "VALIDATION_ERROR"),
        (404, "LESSON_NOT_AVAILABLE"), (409, "MEDIA_NOT_READY"), (403, "ACCESS_DENIED"),
        (503, "ACCESS_DECISION_UNAVAILABLE"), (422, "WATERMARK_UNAVAILABLE"),
        (404, "SESSION_NOT_FOUND"), (410, "SESSION_EXPIRED"),
        (404, "PLAYBACK_SESSION_NOT_FOUND"), (410, "PLAYBACK_SESSION_EXPIRED"),
    ];

    public Task<PlaybackProxyResult> SendAsync(HttpMethod method, string path, string accessToken, CancellationToken cancellationToken)
        => SendAsync(method, path, accessToken, null, null, cancellationToken);

    public async Task<PlaybackProxyResult> SendAsync(HttpMethod method, string path, string accessToken, HttpContent? content, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "internal/v1/" + path);
        request.Headers.Authorization = new("Bearer", accessToken);
        if (content is not null) request.Content = content;

        using var cts = timeout.HasValue ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        if (timeout.HasValue) cts!.CancelAfter(timeout.Value);
        var effectiveToken = cts?.Token ?? cancellationToken;

        try
        {
            using var response = await client.SendAsync(request, effectiveToken);
            var body = await response.Content.ReadAsByteArrayAsync(effectiveToken);
            var status = (int)response.StatusCode;
            var contentType = response.Content.Headers.ContentType?.ToString();
            if (status is 200 or 201)
                return new(status, body, contentType ?? "application/octet-stream");
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(502, Code: "UPSTREAM_UNAVAILABLE");
            var code = document.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return code is not null && KnownErrors.Contains((status, code))
                ? new(status, Code: code,
                    Reason: document.RootElement.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                        && reason.GetString() is "no-grant" or "grant-ended" ? reason.GetString() : null,
                    AccessEndedAt: document.RootElement.TryGetProperty("accessEndedAt", out var ended) && ended.ValueKind == JsonValueKind.String
                        && ended.TryGetDateTimeOffset(out var date) ? date : null) : new(502, Code: "UPSTREAM_UNAVAILABLE");
        }
        catch (HttpRequestException) { return new(502, Code: "UPSTREAM_UNAVAILABLE"); }
        catch (JsonException) { return new(502, Code: "UPSTREAM_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, Code: "UPSTREAM_TIMEOUT"); }
    }
}
