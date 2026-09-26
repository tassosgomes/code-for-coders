using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStaffInvitation;

public sealed class LookupStaffInvitationInputValidator : AbstractValidator<LookupStaffInvitationInput>
{
    public LookupStaffInvitationInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Token).NotEmpty().MaximumLength(512);
    }
}
