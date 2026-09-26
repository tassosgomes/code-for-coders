using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;

public sealed class ValidateStaffSessionInputValidator : AbstractValidator<ValidateStaffSessionInput>
{
    public ValidateStaffSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
    }
}
