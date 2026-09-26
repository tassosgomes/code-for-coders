using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;

public sealed class RevokeStaffSessionInputValidator : AbstractValidator<RevokeStaffSessionInput>
{
    public RevokeStaffSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(256);
    }
}
