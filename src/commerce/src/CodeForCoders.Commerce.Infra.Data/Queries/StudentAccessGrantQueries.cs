using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class StudentAccessGrantQueries(CommerceDbContext db, TimeProvider clock) : IStudentAccessGrantQueries
{
    public async Task<StudentAccessGrantPage> ListAsync(StudentAccessGrantsQuery input, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var query = db.AccessGrants.AsNoTracking().Where(grant => grant.StudentId == input.StudentId);
        var total = await query.CountAsync(cancellationToken);
        var data = await query.OrderByDescending(grant => grant.GrantedAt).ThenByDescending(grant => grant.Id)
            .Skip((input.Page - 1) * input.Size).Take(input.Size)
            .Select(grant => new StudentAccessGrant(grant.Id, grant.StudentId, grant.CourseId,
                db.EntitlementCourseViews.Where(course => course.CourseId == grant.CourseId).Select(course => course.Title).FirstOrDefault() ?? "",
                grant.Origin, grant.ExpiresAt <= now ? "expired" : "active", new StudentAccessPeriod(grant.PeriodType, grant.PeriodMonths),
                grant.GrantedAt, grant.EndsOn, grant.ExpiresAt, grant.Origin == "courtesy" ? grant.Reason : null))
            .ToListAsync(cancellationToken);
        return new(data, new(input.Page, input.Size, total, (int)Math.Ceiling((double)total / input.Size)));
    }
}
