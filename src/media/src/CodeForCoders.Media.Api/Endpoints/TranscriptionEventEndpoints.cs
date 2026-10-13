using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;

namespace CodeForCoders.Media.Api.Endpoints;

public static class TranscriptionEventEndpoints
{
    public static void MapTranscriptionEventEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/webhooks/v1/transcription-events", ReceiveAsync)
            .WithName("receiveTranscriptionEvent").AllowAnonymous();

    private static async Task<IResult> ReceiveAsync(HttpContext context, IReceiveTranscriptionEvent useCase, CancellationToken cancellationToken)
    {
        var body = new byte[ReceiveTranscriptionEventInput.MaximumBodyBytes + 1];
        var length = 0;
        while (length < body.Length)
        {
            var read = await context.Request.Body.ReadAsync(body.AsMemory(length), cancellationToken);
            if (read == 0) break;
            length += read;
        }
        if (length > ReceiveTranscriptionEventInput.MaximumBodyBytes)
        {
            using var stage = MediaTelemetry.BeginTranscriptionStage("webhook.receive");
            MediaTelemetry.RecordTranscriptionWebhook("invalid-request");
            throw new MediaApiException(400, "INVALID_REQUEST", "The transcription event exceeds the body limit.");
        }
        await useCase.ExecuteAsync(new(body[..length], context.Request.Headers["webhook-id"].ToString(),
            context.Request.Headers["webhook-timestamp"].ToString(), context.Request.Headers["webhook-signature"].ToString()), cancellationToken);
        return Results.NoContent();
    }
}
