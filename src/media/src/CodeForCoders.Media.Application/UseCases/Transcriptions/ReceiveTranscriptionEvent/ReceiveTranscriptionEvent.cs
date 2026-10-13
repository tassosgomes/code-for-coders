using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Application.UseCases.Transcriptions.ReceiveTranscriptionEvent;

public sealed class ReceiveTranscriptionEvent(
    TranscriptionWebhookSignature signature,
    ITranscriptionEventRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IReceiveTranscriptionEvent
{
    public async Task ExecuteAsync(ReceiveTranscriptionEventInput input, CancellationToken cancellationToken)
    {
        using var stage = MediaTelemetry.BeginTranscriptionStage("webhook.receive");
        try { await ReceiveAsync(input, stage.Activity, cancellationToken); }
        catch (MediaApiException error)
        {
            MediaTelemetry.RecordTranscriptionWebhook(error.StatusCode switch
            {
                401 => "signature-invalid",
                400 => "invalid-request",
                _ => "storage-unavailable",
            });
            throw;
        }
    }

    private async Task ReceiveAsync(ReceiveTranscriptionEventInput input, Activity? activity, CancellationToken cancellationToken)
    {
        if (input.Body.Length > ReceiveTranscriptionEventInput.MaximumBodyBytes) throw InvalidEvent();
        if (!signature.IsValid(input)) throw new MediaApiException(401, "SIGNATURE_INVALID", "Invalid webhook signature.");
        using var document = Parse(input.Body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || ReadString(root, "eventId") != input.EventId
            || string.IsNullOrWhiteSpace(ReadString(root, "jobId"))
            || ReadString(root, "status") is not ("completed" or "failed")) throw InvalidEvent();
        var jobId = ReadString(root, "jobId")!;
        var reference = ReadString(root, "clientReference");
        var id = Guid.TryParse(reference, out var parsed) ? parsed : (Guid?)null;
        var now = clock.GetUtcNow();
        var outcome = "duplicate";
        var recorded = await repository.RecordAsync(new(input.EventId, id, now), async (attempt, token) =>
        {
            outcome = attempt is not null && attempt.Status is "queued" or "working" or "waiting"
                && (attempt.WhisperJobId is null || attempt.WhisperJobId == jobId) ? "signaled" : "ignored";
            if (attempt is not null)
            {
                activity?.SetTag("video.id", attempt.VideoId.ToString("D"));
                activity?.SetTag("transcription.id", attempt.TranscriptionId.ToString("D"));
            }
            attempt?.Signal(jobId, now);
            await unitOfWork.CommitAsync(token);
        }, cancellationToken);
        if (!recorded) throw new MediaApiException(503, "TEMPORARILY_UNAVAILABLE", "Webhook storage is temporarily unavailable.");
        MediaTelemetry.RecordTranscriptionWebhook(outcome);
    }

    private static JsonDocument Parse(byte[] body)
    {
        try { return JsonDocument.Parse(body); }
        catch (JsonException) { throw InvalidEvent(); }
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static MediaApiException InvalidEvent() => new(400, "INVALID_REQUEST", "Invalid transcription event.");
}
