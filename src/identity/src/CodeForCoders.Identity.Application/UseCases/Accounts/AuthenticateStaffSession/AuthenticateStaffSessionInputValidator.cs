using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;

public sealed class AuthenticateStaffSessionInputValidator : AbstractValidator<AuthenticateStaffSessionInput>
{
    public AuthenticateStaffSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email)
            .NotEmpty()
            .Must(email => new EmailAddressAttribute().IsValid(email));
        RuleFor(input => input.Password).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(256);
    }
}
