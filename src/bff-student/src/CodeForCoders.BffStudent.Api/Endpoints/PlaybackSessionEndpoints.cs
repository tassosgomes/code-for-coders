using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class PlaybackSessionEndpoints
{
    public static void MapPlaybackSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/lessons/{lessonId:guid}/playback-sessions", OpenAsync);
        endpoints.MapPost("/api/v1/playback-sessions/{sessionId:guid}/renewals", RenewAsync);
        endpoints.MapGet("/api/v1/playback-sessions/{sessionId:guid}/playlist", PlaylistAsync);
        endpoints.MapGet("/api/v1/playback-sessions/{sessionId:guid}/variants/{quality}", VariantAsync);
        endpoints.MapGet("/api/v1/playback-sessions/{sessionId:guid}/key", KeyAsync);
    }

    private static Task<IResult> OpenAsync(Guid lessonId, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
        => ProxyAsync(HttpMethod.Post, $"lessons/{lessonId:D}/playback-sessions", context, client, cancellationToken);
    private static Task<IResult> RenewAsync(Guid sessionId, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
        => ProxyAsync(HttpMethod.Post, $"playback-sessions/{sessionId:D}/renewals", context, client, cancellationToken);
    private static Task<IResult> PlaylistAsync(Guid sessionId, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
        => ProxyAsync(HttpMethod.Get, $"playback-sessions/{sessionId:D}/playlist", context, client, cancellationToken);
    private static Task<IResult> VariantAsync(Guid sessionId, string quality, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
        => ProxyAsync(HttpMethod.Get, $"playback-sessions/{sessionId:D}/variants/{Uri.EscapeDataString(quality)}", context, client, cancellationToken);
    private static Task<IResult> KeyAsync(Guid sessionId, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
        => ProxyAsync(HttpMethod.Get, $"playback-sessions/{sessionId:D}/key", context, client, cancellationToken);

    private static async Task<IResult> ProxyAsync(HttpMethod method, string path, HttpContext context, IPlaybackMediaClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await client.SendAsync(method, path, BffSessionContext.GetAccessToken(context)!, cancellationToken);
        if (result.StatusCode is 200 or 201)
        {
            context.Response.StatusCode = result.StatusCode;
            if (result.StatusCode == 201)
            {
                using var document = System.Text.Json.JsonDocument.Parse(result.Body!);
                context.Response.Headers.Location = "/api/v1/playback-sessions/" + document.RootElement.GetProperty("sessionId").GetString();
            }
            return new PlaybackBodyResult(result.Body!, result.ContentType!, result.StatusCode);
        }
        return Results.Problem(statusCode: result.StatusCode, title: "Não foi possível iniciar a aula.", extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Code,
            ["reason"] = result.Reason,
            ["accessEndedAt"] = result.AccessEndedAt,
            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        });
    }

    private sealed class PlaybackBodyResult(byte[] body, string contentType, int status) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            await context.Response.Body.WriteAsync(body, context.RequestAborted);
        }
    }
}
