using FluentValidation;

namespace CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;

public sealed class ConfirmAuditRecordComplementInputValidator : AbstractValidator<ConfirmAuditRecordComplementInput>
{
    public ConfirmAuditRecordComplementInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.RecordId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
        RuleFor(input => input.Explanation)
            .Must(explanation => explanation is not null
                && explanation.Length is >= 1 and <= 1000
                && !string.IsNullOrWhiteSpace(explanation))
            .WithMessage("The audit complement explanation is invalid.");
    }
}
