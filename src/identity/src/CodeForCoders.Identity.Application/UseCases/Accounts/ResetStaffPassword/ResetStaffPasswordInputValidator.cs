using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;

public sealed class ResetStaffPasswordInputValidator : AbstractValidator<ResetStaffPasswordInput>
{
    public ResetStaffPasswordInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Token).NotEmpty();
        RuleFor(input => input.NewPassword).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
