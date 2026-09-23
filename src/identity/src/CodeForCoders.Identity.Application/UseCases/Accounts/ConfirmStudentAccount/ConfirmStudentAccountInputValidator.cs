using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;

public sealed class ConfirmStudentAccountInputValidator : AbstractValidator<ConfirmStudentAccountInput>
{
    public ConfirmStudentAccountInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Token).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
