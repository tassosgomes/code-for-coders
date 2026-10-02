using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IAccessDecisionQueries
{
    Task<AccessGrantValiditySummary?> FindAsync(AccessDecisionQuery input, CancellationToken cancellationToken);
}
