namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;

public sealed record RecordConsumedAuditEventOutput(
    Guid EventId,
    DateTimeOffset RecordedOn);
