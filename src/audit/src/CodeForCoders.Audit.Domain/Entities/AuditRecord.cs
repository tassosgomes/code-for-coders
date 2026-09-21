using CodeForCoders.Audit.Domain.SeedWork;

namespace CodeForCoders.Audit.Domain.Entities;

/// <summary>
/// An append-only technical record of an event consumed from the broker.
/// </summary>
public sealed class AuditRecord
{
    public const int SourceServiceMaxLength = 100;
    public const int EventTypeMaxLength = 200;
    public const int PayloadMaxLength = 100_000;

    private AuditRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string SourceService { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; private set; }

    public DateTimeOffset RecordedOn { get; private set; }

    public static AuditRecord Create(
        Guid eventId,
        Guid tenantId,
        string sourceService,
        string eventType,
        string payload,
        DateTimeOffset occurredOn,
        DateTimeOffset recordedOn)
    {
        if (eventId == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Audit event identifiers must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(sourceService)
            || sourceService.Length > SourceServiceMaxLength
            || string.IsNullOrWhiteSpace(eventType)
            || eventType.Length > EventTypeMaxLength
            || string.IsNullOrWhiteSpace(payload)
            || payload.Length > PayloadMaxLength)
        {
            throw new EntityValidationException("Audit event metadata is outside the technical limits.");
        }

        return new AuditRecord
        {
            Id = eventId,
            TenantId = tenantId,
            SourceService = sourceService,
            EventType = eventType,
            Payload = payload,
            OccurredOn = occurredOn,
            RecordedOn = recordedOn,
        };
    }
}
