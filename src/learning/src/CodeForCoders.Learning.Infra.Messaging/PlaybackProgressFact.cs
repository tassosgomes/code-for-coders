using System.Text.Json;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed record PlaybackProgressFact(Guid EventId, Guid TenantId, Guid SessionId, Guid StudentId,
    Guid CourseId, Guid LessonId, int Sequence, int PositionSeconds, string Reason, DateTimeOffset OccurredAt)
{
    public const string Route = "midia.reproducao-avancou.v1";
    private static readonly HashSet<string> Fields = ["eventId", "tenantId", "sessionId", "studentId", "courseId",
        "lessonId", "sequence", "positionSeconds", "reason", "occurredAt"];

    public static PlaybackProgressFact Parse(ReadOnlyMemory<byte> body, string routingKey, string? messageId)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || routingKey != Route
            || root.EnumerateObject().Any(property => !Fields.Contains(property.Name))
            || root.EnumerateObject().Count() != Fields.Count)
            throw new JsonException("Invalid playback progress fields.");
        var eventId = RequiredId(root, "eventId");
        if (!Guid.TryParse(messageId, out var envelopeId) || envelopeId != eventId
            || !root.TryGetProperty("sequence", out var sequenceValue) || !sequenceValue.TryGetInt32(out var sequence) || sequence < 1
            || !root.TryGetProperty("positionSeconds", out var positionValue) || !positionValue.TryGetInt32(out var position) || position is < 0 or > 43200
            || !root.TryGetProperty("reason", out var reason) || reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString())
            || !root.TryGetProperty("occurredAt", out var time) || time.ValueKind != JsonValueKind.String || !time.TryGetDateTimeOffset(out var occurredAt))
            throw new JsonException("Invalid playback progress fact or message identity.");
        return new(eventId, RequiredId(root, "tenantId"), RequiredId(root, "sessionId"), RequiredId(root, "studentId"),
            RequiredId(root, "courseId"), RequiredId(root, "lessonId"), sequence, position, reason.GetString()!, occurredAt.ToUniversalTime());
    }

    private static Guid RequiredId(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || !value.TryGetGuid(out var id) || id == Guid.Empty)
            throw new JsonException("Invalid playback progress identifier.");
        return id;
    }
}
