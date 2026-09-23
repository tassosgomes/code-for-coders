using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;

public sealed class RevokeStudentSessionInputValidator : AbstractValidator<RevokeStudentSessionInput>
{
    public RevokeStudentSessionInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(256);
    }
}
