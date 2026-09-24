using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;

public sealed class RequestStudentPasswordResetInputValidator : AbstractValidator<RequestStudentPasswordResetInput>
{
    public RequestStudentPasswordResetInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email).NotEmpty().EmailAddress();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
