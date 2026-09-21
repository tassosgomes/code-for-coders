namespace CodeForCoders.Audit.Contracts;

/// <summary>
/// Minimal integration event consumed by audit. The payload is retained as evidence and is never mutated.
/// </summary>
public sealed record AuditEventV1(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredOn,
    string SourceService,
    string EventType,
    string Payload);
