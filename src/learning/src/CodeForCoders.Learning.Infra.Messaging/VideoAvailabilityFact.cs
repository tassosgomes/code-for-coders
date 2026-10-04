using System.Text.Json;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed record VideoAvailabilityFact(Guid EventId, Guid TenantId, Guid VideoId, DateTimeOffset OccurredAt, bool IsReady, int? DurationSeconds = null)
{
    public const string ReadyRoute = "midia.ativo-pronto.v1";
    public const string FailedRoute = "midia.preparacao-falhou.v1";

    public static VideoAvailabilityFact Parse(ReadOnlyMemory<byte> body, string routingKey, string? messageId)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (routingKey is not (ReadyRoute or FailedRoute)
            || !root.TryGetProperty("eventId", out var eventValue) || !eventValue.TryGetGuid(out var eventId) || eventId == Guid.Empty
            || !root.TryGetProperty("tenantId", out var tenantValue) || !tenantValue.TryGetGuid(out var tenantId) || tenantId == Guid.Empty
            || !root.TryGetProperty("videoId", out var videoValue) || !videoValue.TryGetGuid(out var videoId) || videoId == Guid.Empty
            || !root.TryGetProperty("occurredAt", out var dateValue) || !dateValue.TryGetDateTimeOffset(out var occurredAt)
            || !Guid.TryParse(messageId, out var envelopeId) || envelopeId != eventId)
            throw new JsonException("Invalid video availability fact or message identity.");
        if (routingKey == ReadyRoute && (!root.TryGetProperty("durationSeconds", out var duration)
            || !duration.TryGetInt32(out var seconds) || seconds <= 0))
            throw new JsonException("Invalid ready video duration.");
        if (routingKey == FailedRoute && (!root.TryGetProperty("reason", out var reason)
            || reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString())))
            throw new JsonException("Invalid video failure reason.");
        return new(eventId, tenantId, videoId, occurredAt.ToUniversalTime(), routingKey == ReadyRoute,
            routingKey == ReadyRoute ? root.GetProperty("durationSeconds").GetInt32() : null);
    }
}
