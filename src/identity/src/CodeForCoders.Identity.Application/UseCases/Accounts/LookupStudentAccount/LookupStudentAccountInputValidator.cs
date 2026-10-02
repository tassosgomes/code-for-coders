using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStudentAccount;

public sealed class LookupStudentAccountInputValidator : AbstractValidator<LookupStudentAccountInput>
{
    public LookupStudentAccountInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
        RuleFor(input => input.Email).NotEmpty().MaximumLength(254).EmailAddress();
    }
}
