using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class StudentCourseAccessQueries(CommerceDbContext db) : IStudentCourseAccessQueries
{
    private const int MaximumCourses = 500;

    public async Task<IReadOnlyList<StudentCourseAccess>> ListAsync(StudentCourseAccessQuery input, CancellationToken cancellationToken)
        => await db.AccessGrants.AsNoTracking().Where(grant => grant.StudentId == input.StudentId)
            .GroupBy(grant => grant.CourseId).OrderBy(grants => grants.Key).Take(MaximumCourses)
            .Select(grants => new StudentCourseAccess(grants.Key,
                grants.Any(grant => grant.Status == "active" && (grant.ExpiresAt == null || grant.ExpiresAt > input.Now)) ? "active" : "ended",
                grants.Where(grant => grant.Status == "active" && (grant.ExpiresAt == null || grant.ExpiresAt > input.Now))
                    .Max(grant => (DateTimeOffset?)grant.GrantedAt),
                grants.Any(grant => grant.Status == "active" && (grant.ExpiresAt == null || grant.ExpiresAt > input.Now)) ? null : grants.Max(grant => grant.EndsOn),
                grants.Any(grant => grant.Status == "active" && (grant.ExpiresAt == null || grant.ExpiresAt > input.Now)) ? null : grants.Max(grant => grant.ExpiresAt),
                grants.Any(grant => grant.Status == "active" && (grant.ExpiresAt == null || grant.ExpiresAt > input.Now)) ? null : "grant-ended"))
            .ToListAsync(cancellationToken);
}
