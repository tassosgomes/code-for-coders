using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class AccessDecisionQueries(CommerceDbContext db) : IAccessDecisionQueries
{
    public Task<AccessGrantValiditySummary?> FindAsync(AccessDecisionQuery input, CancellationToken cancellationToken)
        => db.AccessGrants.AsNoTracking()
            .Where(grant => grant.StudentId == input.StudentId && grant.CourseId == input.CourseId && grant.Status == "active")
            .GroupBy(grant => 1)
            .Select(grants => new AccessGrantValiditySummary(grants.Any(grant => grant.ExpiresAt == null),
                grants.Max(grant => grant.ExpiresAt)))
            .SingleOrDefaultAsync(cancellationToken);
}
