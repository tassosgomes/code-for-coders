namespace CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;

public enum ConfirmAuditRecordComplementStatus
{
    Accepted,
    Conflict,
    InvalidExplanation,
}

public sealed record ConfirmAuditRecordComplementOutput(
    ConfirmAuditRecordComplementStatus Status,
    Guid? ConfirmationId);
