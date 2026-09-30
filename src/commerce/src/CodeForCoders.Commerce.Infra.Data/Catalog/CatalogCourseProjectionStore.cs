using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class CatalogCourseProjectionStore(CommerceDbContext dbContext, ITenantContext tenantContext)
    : ICatalogCourseProjectionStore
{
    public async Task<bool> ApplyAsync(PublishedCourseSnapshot snapshot, CancellationToken cancellationToken)
    {
        tenantContext.Set(snapshot.TenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize the first insert too: a row lock cannot protect a course that does not exist yet.
        var lockKey = $"{snapshot.TenantId:D}/{snapshot.CourseId:D}";
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var rows = await dbContext.CatalogCourseViews.FromSql(
            $"SELECT * FROM catalog.course_views WHERE tenant_id = {snapshot.TenantId} AND course_id = {snapshot.CourseId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var course = rows.SingleOrDefault();
        var applied = course is null || course.Apply(snapshot);
        if (course is null) dbContext.CatalogCourseViews.Add(CatalogCourseView.Create(snapshot));
        if (applied) await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return applied;
    }
}
