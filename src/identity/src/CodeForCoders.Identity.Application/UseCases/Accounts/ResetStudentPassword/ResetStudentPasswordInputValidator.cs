using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;

public sealed class ResetStudentPasswordInputValidator : AbstractValidator<ResetStudentPasswordInput>
{
    public ResetStudentPasswordInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Token).NotEmpty();
        RuleFor(input => input.NewPassword).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
