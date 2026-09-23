using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;

public sealed class RequestStudentAccountConfirmationInputValidator : AbstractValidator<RequestStudentAccountConfirmationInput>
{
    public RequestStudentAccountConfirmationInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email).NotEmpty().EmailAddress();
        RuleFor(input => input.IdempotencyKey).NotEmpty();
    }
}
