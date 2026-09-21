using CodeForCoders.Audit.Contracts;

namespace CodeForCoders.Audit.Application.Interfaces;

/// <summary>
/// Application boundary used by infrastructure consumers to append an incoming audit event.
/// The consumer does not depend on a concrete use case or its input/output DTOs.
/// </summary>
public interface IAuditEventRecorder
{
    Task RecordAsync(AuditEventV1 auditEvent, CancellationToken cancellationToken);
}
