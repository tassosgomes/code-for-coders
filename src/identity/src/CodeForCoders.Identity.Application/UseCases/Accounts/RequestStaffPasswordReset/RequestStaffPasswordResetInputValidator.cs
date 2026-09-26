using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStaffPasswordReset;

public sealed class RequestStaffPasswordResetInputValidator : AbstractValidator<RequestStaffPasswordResetInput>
{
    public RequestStaffPasswordResetInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
