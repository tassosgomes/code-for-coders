using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;

public sealed class ResolveAuditIdentityReferencesInputValidator : AbstractValidator<ResolveAuditIdentityReferencesInput>
{
    private static readonly string[] ReferenceTypes = ["conta-interna", "convite-interno", "conta-aluno"];

    public ResolveAuditIdentityReferencesInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.References).NotEmpty().Must(references => references.Count <= 50);
        RuleForEach(input => input.References).ChildRules(reference =>
        {
            reference.RuleFor(item => item.Id).NotEmpty();
            reference.RuleFor(item => item.Type).Must(type => ReferenceTypes.Contains(type, StringComparer.Ordinal));
        });
        RuleFor(input => input.References)
            .Must(references => references.Distinct().Count() == references.Count);
    }
}
