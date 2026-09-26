using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;

public sealed class CreateStaffInvitationInputValidator : AbstractValidator<CreateStaffInvitationInput>
{
    public CreateStaffInvitationInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorAccountId).NotEmpty();
        RuleFor(input => input.Email)
            .NotEmpty()
            .MaximumLength(320)
            .Must(email => email is not null && new EmailAddressAttribute().IsValid(email.Trim()));
        RuleFor(input => input.Role).NotEmpty().MaximumLength(32);
        RuleFor(input => input.Reason).MaximumLength(1000);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
