using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class EntitlementCourseProjectionStore(CommerceDbContext dbContext, ITenantContext tenantContext)
    : IEntitlementCourseProjectionStore
{
    public async Task<bool> ApplyAsync(PublishedCourseSnapshot snapshot, CancellationToken cancellationToken)
    {
        tenantContext.Set(snapshot.TenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Protect concurrent first deliveries before a row exists, independently of Catalog.
        var lockKey = $"entitlement/{snapshot.TenantId:D}/{snapshot.CourseId:D}";
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var rows = await dbContext.EntitlementCourseViews.FromSql(
            $"SELECT * FROM entitlement.course_views WHERE tenant_id = {snapshot.TenantId} AND course_id = {snapshot.CourseId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var course = rows.SingleOrDefault();
        var applied = course is null || course.Apply(snapshot);
        if (course is null) dbContext.EntitlementCourseViews.Add(EntitlementCourseView.Create(snapshot));
        if (applied) await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return applied;
    }
}
