using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaDbContext(
    DbContextOptions<MediaDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<CourseReferences.CourseReferenceVersion> CourseReferenceVersions => Set<CourseReferences.CourseReferenceVersion>();
    public DbSet<CourseReferences.CourseVideoReference> CourseVideoReferences => Set<CourseReferences.CourseVideoReference>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Video> Videos => Set<Video>();

    public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();

    public DbSet<OperationIdempotencyRecord> OperationIdempotencyRecords => Set<OperationIdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(MediaSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
        modelBuilder.Entity<CourseReferences.CourseReferenceVersion>().HasQueryFilter(reference => tenantContext.TenantId.HasValue && reference.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CourseReferences.CourseVideoReference>().HasQueryFilter(reference => tenantContext.TenantId.HasValue && reference.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Video>().HasQueryFilter(
            video => tenantContext.TenantId.HasValue && video.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VideoUpload>().HasQueryFilter(
            upload => tenantContext.TenantId.HasValue && upload.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OperationIdempotencyRecord>().HasQueryFilter(
            record => tenantContext.TenantId.HasValue && record.TenantId == tenantContext.TenantId.Value);
    }
}
