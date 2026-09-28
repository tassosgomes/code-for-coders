namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface IAuditComplementConfirmationStore
{
    Task<AuditComplementConfirmationWriteResult?> FindExistingAsync(
        AuditComplementConfirmationDraft draft,
        CancellationToken cancellationToken);

    Task<AuditComplementConfirmationWriteResult> ConfirmAsync(
        AuditComplementConfirmationDraft draft,
        CancellationToken cancellationToken);
}

public sealed record AuditComplementConfirmationDraft(
    Guid TenantId,
    Guid ActorId,
    Guid RecordId,
    Guid IdempotencyKey,
    Guid ConfirmationId,
    string Explanation,
    DateTimeOffset ConfirmedAt,
    string? TraceParent);

public enum AuditComplementConfirmationWriteStatus
{
    Accepted,
    Replay,
    Conflict,
}

public sealed record AuditComplementConfirmationWriteResult(
    AuditComplementConfirmationWriteStatus Status,
    Guid ConfirmationId);
