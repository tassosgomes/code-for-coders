using CodeForCoders.Identity.Application.Interfaces;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;

public sealed class ResolveAuditIdentityReferences(
    IAuditIdentityReferenceQueries queries,
    IValidator<ResolveAuditIdentityReferencesInput> validator) : IResolveAuditIdentityReferences
{
    public async Task<ResolveAuditIdentityReferencesOutput> ExecuteAsync(
        ResolveAuditIdentityReferencesInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var labels = await queries.ResolveLabelsAsync(input.TenantId, input.References, cancellationToken);
        return new ResolveAuditIdentityReferencesOutput(input.References
            .Select(reference => new ResolvedAuditIdentityReference(
                reference.Type,
                reference.Id,
                labels.GetValueOrDefault(reference)))
            .ToArray());
    }
}
