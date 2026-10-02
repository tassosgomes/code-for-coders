using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;

public sealed class DecideAccess(IAccessDecisionQueries queries, TimeProvider clock) : IDecideAccess
{
    public async Task<DecideAccessOutput> ExecuteAsync(DecideAccessInput input, CancellationToken cancellationToken)
    {
        var grants = await queries.FindAsync(new(input.StudentId, input.CourseId), cancellationToken);
        return DecideAccessOutput.FromAccessDecision(AccessDecisionRules.Decide(grants, clock.GetUtcNow()));
    }
}
