using FluentValidation;
namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveStudentAccounts;

public sealed class ResolveStudentAccountsInputValidator : AbstractValidator<ResolveStudentAccountsInput>
{
    public ResolveStudentAccountsInputValidator()
    {
        RuleFor(input => input.StudentIds).Must(ids => ids is { Count: > 0 and <= 50 }
            && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count);
    }
}
