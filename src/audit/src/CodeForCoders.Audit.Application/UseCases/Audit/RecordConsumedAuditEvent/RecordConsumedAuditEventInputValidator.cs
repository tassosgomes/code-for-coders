using FluentValidation;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;

public sealed class RecordConsumedAuditEventInputValidator
    : AbstractValidator<RecordConsumedAuditEventInput>
{
    public RecordConsumedAuditEventInputValidator()
    {
        RuleFor(input => input.Event.EventId).NotEmpty();
        RuleFor(input => input.Event.TenantId).NotEmpty();
        RuleFor(input => input.Event.SourceService).NotEmpty().MaximumLength(100);
        RuleFor(input => input.Event.EventType).NotEmpty().MaximumLength(200);
        RuleFor(input => input.Event.Payload).NotEmpty().MaximumLength(100_000);
    }
}
