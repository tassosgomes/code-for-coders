using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;

public sealed class AuthenticateStudentSessionInputValidator : AbstractValidator<AuthenticateStudentSessionInput>
{
    public AuthenticateStudentSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email)
            .NotEmpty()
            .Must(email => new EmailAddressAttribute().IsValid(email));
        RuleFor(input => input.Password).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(256);
    }
}
