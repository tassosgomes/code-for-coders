using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data.Configuration;
using CodeForCoders.Learning.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data;

public sealed class LearningDbContext(
    DbContextOptions<LearningDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<CourseVersion> CourseVersions => Set<CourseVersion>();

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<VideoProjection.ProjectedVideo> ProjectedVideos => Set<VideoProjection.ProjectedVideo>();
    public DbSet<VideoProjection.VideoFactReceipt> VideoFactReceipts => Set<VideoProjection.VideoFactReceipt>();
    public DbSet<CourseEditReceipt> CourseEditReceipts => Set<CourseEditReceipt>();
    public DbSet<CourseCreationReceipt> CourseCreationReceipts => Set<CourseCreationReceipt>();

    public DbSet<ContentOutboxMessage> ContentOutboxMessages => Set<ContentOutboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(LearningSchemas.Content);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
        modelBuilder.Entity<VideoProjection.ProjectedVideo>().HasQueryFilter(video => tenantContext.TenantId.HasValue && video.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VideoProjection.VideoFactReceipt>().HasQueryFilter(receipt => tenantContext.TenantId.HasValue && receipt.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CourseVersion>().HasQueryFilter(version => tenantContext.TenantId.HasValue && version.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Course>().HasQueryFilter(course => tenantContext.TenantId.HasValue && course.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CourseEditReceipt>().HasQueryFilter(receipt => tenantContext.TenantId.HasValue && receipt.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CourseCreationReceipt>().HasQueryFilter(receipt => tenantContext.TenantId.HasValue && receipt.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<ContentOutboxMessage>().HasQueryFilter(message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
    }
}
