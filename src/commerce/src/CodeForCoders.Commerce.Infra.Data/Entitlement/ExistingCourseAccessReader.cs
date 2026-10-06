using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class ExistingCourseAccessReader(CommerceDbContext db, TimeProvider clock) : IExistingCourseAccessQueries
{
    public async Task<ExistingCourseAccess?> FindAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var grant = await db.AccessGrants.AsNoTracking().Where(item => item.StudentId == studentId && item.CourseId == courseId
            && item.Status == "active" && item.GrantedAt <= now && (item.ExpiresAt == null || item.ExpiresAt > now))
            .OrderByDescending(item => item.ExpiresAt == null).ThenByDescending(item => item.ExpiresAt).ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return grant is null ? null : new(grant.Origin, new(grant.ExpiresAt is null ? "lifetime" : "until", grant.EndsOn));
    }
}
