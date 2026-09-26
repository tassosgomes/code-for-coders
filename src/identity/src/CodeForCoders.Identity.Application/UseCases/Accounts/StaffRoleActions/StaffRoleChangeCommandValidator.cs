using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

public sealed class StaffRoleChangeCommandValidator : AbstractValidator<StaffRoleChangeCommand>
{
    public StaffRoleChangeCommandValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorAccountId).NotEmpty();
        RuleFor(input => input.TargetAccountId).NotEmpty();
        RuleFor(input => input.FromRole).NotEmpty().MaximumLength(32);
        RuleFor(input => input.ToRole).NotEmpty().MaximumLength(32);
        RuleFor(input => input.Reason).NotNull().MaximumLength(1000);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
