using CodeForCoders.Commerce.Domain.Entities;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;

public sealed record CourtesyGrant(Guid GrantId, Guid StudentId, Guid CourseId, string CourseTitle, string Origin,
    string Status, AccessPeriod AccessPeriod, DateTimeOffset GrantedAt, DateOnly? EndsOn, DateTimeOffset? ExpiresAt, string? Reason)
{
    public static CourtesyGrant FromAccessGrant(AccessGrant grant, string title, DateTimeOffset now)
        => new(grant.Id, grant.StudentId, grant.CourseId, title, grant.Origin,
            grant.ExpiresAt <= now ? "expired" : grant.Status, new(grant.PeriodType, grant.PeriodMonths),
            grant.GrantedAt, grant.EndsOn, grant.ExpiresAt, grant.Reason);
}
