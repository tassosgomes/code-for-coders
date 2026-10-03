using System.Security.Claims;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.RenewPlaybackSession;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackResource;

namespace CodeForCoders.Media.Api.Endpoints;

public static class PlaybackSessionEndpoints
{
    public static void MapPlaybackSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var lessons = endpoints.MapGroup("/internal/v1/lessons").RequireAuthorization(MediaAuthorization.PlaybackPolicyName);
        lessons.MapPost("/{lessonId:guid}/playback-sessions", OpenAsync);
        var sessions = endpoints.MapGroup("/internal/v1/playback-sessions").RequireAuthorization(MediaAuthorization.PlaybackPolicyName);
        sessions.MapPost("/{sessionId:guid}/renewals", RenewAsync);
        sessions.MapGet("/{sessionId:guid}/playlist", PlaylistAsync);
        sessions.MapGet("/{sessionId:guid}/variants/{quality}", VariantAsync);
        sessions.MapGet("/{sessionId:guid}/key", KeyAsync);
    }

    private static async Task<IResult> OpenAsync(Guid lessonId, HttpContext context, IOpenPlaybackSession useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var result = await useCase.ExecuteAsync(new(
            Guid.Parse(context.User.FindFirstValue("tenantId")!),
            Guid.Parse(context.User.FindFirstValue("sub")!), lessonId, context.User.FindFirstValue("email")), cancellationToken);
        return Results.Created($"/playback-sessions/{result.SessionId:D}", result);
    }

    private static async Task<IResult> RenewAsync(Guid sessionId, HttpContext context, IRenewPlaybackSession useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(Guid.Parse(context.User.FindFirstValue("sub")!),
            sessionId, context.User.FindFirstValue("email")), cancellationToken));
    }

    private static Task<IResult> PlaylistAsync(Guid sessionId, HttpContext context, IGetPlaybackResource useCase, CancellationToken cancellationToken)
        => DeliverAsync(new(Guid.Parse(context.User.FindFirstValue("sub")!), sessionId, "playlist", null), context, useCase, cancellationToken);
    private static Task<IResult> VariantAsync(Guid sessionId, string quality, HttpContext context, IGetPlaybackResource useCase, CancellationToken cancellationToken)
        => DeliverAsync(new(Guid.Parse(context.User.FindFirstValue("sub")!), sessionId, "variant", quality), context, useCase, cancellationToken);
    private static Task<IResult> KeyAsync(Guid sessionId, HttpContext context, IGetPlaybackResource useCase, CancellationToken cancellationToken)
        => DeliverAsync(new(Guid.Parse(context.User.FindFirstValue("sub")!), sessionId, "key", null), context, useCase, cancellationToken);
    private static async Task<IResult> DeliverAsync(GetPlaybackResourceInput input, HttpContext context, IGetPlaybackResource useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var output = await useCase.ExecuteAsync(input, cancellationToken);
        return new PlaybackResourceResult(output.Body, output.ContentType);
    }
    private sealed class PlaybackResourceResult(byte[] body, string contentType) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.ContentType = contentType;
            try { await context.Response.Body.WriteAsync(body, context.RequestAborted); }
            finally { if (contentType == "application/octet-stream") System.Security.Cryptography.CryptographicOperations.ZeroMemory(body); }
        }
    }

}
