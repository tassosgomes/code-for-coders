using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;

public sealed class AcceptStaffInvitationInputValidator : AbstractValidator<AcceptStaffInvitationInput>
{
    public AcceptStaffInvitationInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Token).NotEmpty().MaximumLength(512);
        RuleFor(input => input.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(200);
        RuleFor(input => input.Password).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
