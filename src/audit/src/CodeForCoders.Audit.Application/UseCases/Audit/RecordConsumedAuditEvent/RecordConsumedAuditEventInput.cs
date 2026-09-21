using CodeForCoders.Audit.Contracts;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;

public sealed record RecordConsumedAuditEventInput(AuditEventV1 Event);
