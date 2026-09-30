using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.CourseReferences;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class CourseReferenceStore(MediaDbContext context, ITenantContext tenant)
{
    public async Task ApplyAsync(PublishedCourseFact fact, CancellationToken cancellationToken)
    {
        tenant.Set(fact.TenantId);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"{fact.TenantId:D}:{fact.CourseId:D}";
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var current = await context.CourseReferenceVersions.SingleOrDefaultAsync(version => version.CourseId == fact.CourseId, cancellationToken);
        if (current is not null && current.VersionNumber >= fact.VersionNumber) return;
        var existing = await context.CourseVideoReferences.Where(reference => reference.CourseId == fact.CourseId).ToListAsync(cancellationToken);
        context.CourseVideoReferences.RemoveRange(existing);
        await context.SaveChangesAsync(cancellationToken);
        context.CourseVideoReferences.AddRange(fact.References.Select(reference => new CourseVideoReference
        {
            TenantId = fact.TenantId,
            CourseId = fact.CourseId,
            LessonId = reference.LessonId,
            VideoId = reference.VideoId,
        }));
        if (current is null)
        {
            current = new CourseReferenceVersion { TenantId = fact.TenantId, CourseId = fact.CourseId };
            context.CourseReferenceVersions.Add(current);
        }
        current.VersionNumber = fact.VersionNumber;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(CancellationToken.None);
    }
}
