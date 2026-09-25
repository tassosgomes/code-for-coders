using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ProvisionFirstAdministrator;

public sealed class ProvisionFirstAdministratorInputValidator
    : AbstractValidator<ProvisionFirstAdministratorInput>
{
    public ProvisionFirstAdministratorInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.Email).NotEmpty().EmailAddress();
        RuleFor(input => input.Name).NotEmpty().MaximumLength(200);
    }
}
