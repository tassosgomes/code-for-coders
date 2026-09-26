using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;

public sealed class ListPendingStaffInvitationsInputValidator : AbstractValidator<ListPendingStaffInvitationsInput>
{
    public ListPendingStaffInvitationsInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 100);
    }
}
