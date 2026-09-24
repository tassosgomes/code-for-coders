using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;

public sealed class ValidateStudentSessionInputValidator : AbstractValidator<ValidateStudentSessionInput>
{
    public ValidateStudentSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
    }
}
