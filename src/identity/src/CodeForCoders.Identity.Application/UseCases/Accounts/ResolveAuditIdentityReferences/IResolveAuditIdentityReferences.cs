using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;

public interface IResolveAuditIdentityReferences : IUseCase<
    ResolveAuditIdentityReferencesInput,
    ResolveAuditIdentityReferencesOutput>;
