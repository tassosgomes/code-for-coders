using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;

public sealed class RegisterStudentAccountInputValidator : AbstractValidator<RegisterStudentAccountInput>
{
    public RegisterStudentAccountInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Name).Must(name => !string.IsNullOrWhiteSpace(name));
        RuleFor(input => input.Email).NotEmpty().EmailAddress();
        RuleFor(input => input.Password).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
