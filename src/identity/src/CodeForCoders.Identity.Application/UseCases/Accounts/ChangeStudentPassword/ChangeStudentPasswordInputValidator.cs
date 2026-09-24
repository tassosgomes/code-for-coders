using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;

public sealed class ChangeStudentPasswordInputValidator : AbstractValidator<ChangeStudentPasswordInput>
{
    public ChangeStudentPasswordInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
        RuleFor(input => input.CurrentPassword).NotEmpty();
        RuleFor(input => input.NewPassword).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
