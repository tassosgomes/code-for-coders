namespace CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;

public sealed record ConfirmAuditRecordComplementInput(
    Guid TenantId,
    Guid ActorId,
    Guid RecordId,
    Guid IdempotencyKey,
    string? Explanation);
