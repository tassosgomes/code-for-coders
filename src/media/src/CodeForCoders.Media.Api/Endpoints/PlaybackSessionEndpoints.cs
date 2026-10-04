using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.RenewPlaybackSession;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.OpenPlaybackSession;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackResource;
using CodeForCoders.Media.Application.UseCases.PlaybackSessions.RecordPlaybackProgress;

namespace CodeForCoders.Media.Api.Endpoints;

public static class PlaybackSessionEndpoints
{
    public static void MapPlaybackSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var lessons = endpoints.MapGroup("/internal/v1/lessons").RequireAuthorization(MediaAuthorization.PlaybackPolicyName);
        lessons.MapPost("/{lessonId:guid}/playback-sessions", OpenAsync);
        var sessions = endpoints.MapGroup("/internal/v1/playback-sessions").RequireAuthorization(MediaAuthorization.PlaybackPolicyName);
        sessions.MapPost("/{sessionId:guid}/renewals", RenewAsync);
        sessions.MapPost("/{sessionId:guid}/progress", ProgressAsync);
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

    private static async Task<IResult> ProgressAsync(Guid sessionId, HttpContext context, IRecordPlaybackProgress useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        using var reader = new StreamReader(context.Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return ValidationProblem(context);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return ValidationProblem(context);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ValidationProblem(context);
            }

            // Schema: additionalProperties: false
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not ("sequence" or "positionSeconds" or "reason"))
                {
                    return ValidationProblem(context);
                }
            }

            if (!root.TryGetProperty("sequence", out var sequenceProp) || sequenceProp.ValueKind != JsonValueKind.Number || !sequenceProp.TryGetInt32(out var sequence) || sequence < 1)
            {
                return ValidationProblem(context);
            }

            if (!root.TryGetProperty("positionSeconds", out var positionProp) || positionProp.ValueKind != JsonValueKind.Number || !positionProp.TryGetInt32(out var positionSeconds) || positionSeconds < 0 || positionSeconds > 43200)
            {
                return ValidationProblem(context);
            }

            if (!root.TryGetProperty("reason", out var reasonProp) || reasonProp.ValueKind != JsonValueKind.String)
            {
                return ValidationProblem(context);
            }

            var reason = reasonProp.GetString();
            if (reason is not ("heartbeat" or "paused" or "left" or "ended"))
            {
                return ValidationProblem(context);
            }

            var tenantId = Guid.Parse(context.User.FindFirstValue("tenantId")!);
            var studentId = Guid.Parse(context.User.FindFirstValue("sub")!);
            var traceParent = context.Request.Headers["traceparent"].FirstOrDefault()
                ?? System.Diagnostics.Activity.Current?.Id
                ?? context.TraceIdentifier;

            var result = await useCase.ExecuteAsync(new(
                tenantId,
                studentId,
                sessionId,
                sequence,
                positionSeconds,
                reason,
                traceParent), cancellationToken);

            return Results.Ok(new { recorded = result.Recorded });
        }
    }

    private static IResult ValidationProblem(HttpContext context) =>
        Results.Problem(
            statusCode: 400,
            title: "Requisição inválida.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "VALIDATION_ERROR",
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            });
}
