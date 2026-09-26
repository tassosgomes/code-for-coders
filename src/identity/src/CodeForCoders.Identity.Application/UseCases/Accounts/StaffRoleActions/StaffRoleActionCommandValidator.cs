using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

public sealed class StaffRoleActionCommandValidator : AbstractValidator<StaffRoleActionCommand>
{
    public StaffRoleActionCommandValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorAccountId).NotEmpty();
        RuleFor(input => input.TargetAccountId).NotEmpty();
        RuleFor(input => input.Role).NotEmpty().MaximumLength(32);
        RuleFor(input => input.Reason).NotNull().MaximumLength(1000);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
