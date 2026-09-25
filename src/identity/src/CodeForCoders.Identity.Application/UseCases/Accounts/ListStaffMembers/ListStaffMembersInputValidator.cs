using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;

public sealed class ListStaffMembersInputValidator : AbstractValidator<ListStaffMembersInput>
{
    public ListStaffMembersInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorAccountId).NotEmpty();
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 100);
    }
}
