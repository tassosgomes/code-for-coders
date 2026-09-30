using System.Diagnostics;
using System.Text.Json;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.Catalog;
using CodeForCoders.Learning.Infra.Data.Outbox;
using CodeForCoders.Learning.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class CatalogInitialLoad(
    LearningDbContext context,
    ICourseEditStore courseEdits,
    IOptions<CatalogInitialLoadOptions> options,
    ILogger<CatalogInitialLoad> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        // A session lock serializes hosts while each course keeps its own short transaction.
        // Explicitly closing the connection also releases the lock if cancellation interrupts acquisition.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtextextended({CatalogInitialLoadExecution.ExecutionName}, 0))", cancellationToken);
            await ReplayAsync(cancellationToken);
        }
        finally
        {
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtextextended({CatalogInitialLoadExecution.ExecutionName}, 0))", CancellationToken.None);
            }
            finally { await context.Database.CloseConnectionAsync(); }
        }
    }

    private async Task ReplayAsync(CancellationToken cancellationToken)
    {
        if (await context.CatalogInitialLoadExecutions.AnyAsync(item => item.Name == CatalogInitialLoadExecution.ExecutionName, cancellationToken)) return;
        var courses = await context.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(course => course.CurrentVersion != null)
            .OrderBy(course => course.Id).Select(course => new { course.Id, course.TenantId }).ToListAsync(cancellationToken);
        var replayed = 0;
        foreach (var course in courses)
        {
            if (await ReplayCourseAsync(course.Id, course.TenantId, cancellationToken)) replayed++;
            context.ChangeTracker.Clear();
        }
        context.CatalogInitialLoadExecutions.Add(CatalogInitialLoadExecution.Complete(replayed));
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Catalog initial load completed. ReplayedCourseCount: {ReplayedCourseCount}. Execution: {ExecutionName}. MarkerCompleted: {MarkerCompleted}.",
            replayed, CatalogInitialLoadExecution.ExecutionName, true);
    }

    private async Task<bool> ReplayCourseAsync(Guid courseId, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = await courseEdits.LockAsync(courseId, cancellationToken);
        var version = await (from course in context.Courses.IgnoreQueryFilters().AsNoTracking()
                             join current in context.CourseVersions.IgnoreQueryFilters().AsNoTracking()
                                 on new { CourseId = course.Id, VersionNumber = course.CurrentVersion }
                                 equals new { current.CourseId, VersionNumber = (int?)current.VersionNumber }
                             where course.Id == courseId && course.TenantId == tenantId && current.TenantId == tenantId
                             select current).SingleOrDefaultAsync(cancellationToken);
        if (version is null) return false;
        var draft = PublishedCourseFact.FromVersion(version, Activity.Current?.Id);
        context.ContentOutboxMessages.Add(ContentOutboxMessage.CreateReplay(draft, JsonSerializer.Serialize(draft.Payload, JsonOptions)));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CompleteAsync(CancellationToken.None);
        return true;
    }
}
