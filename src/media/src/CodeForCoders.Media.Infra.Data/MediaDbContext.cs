using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Data.Transcriptions;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaDbContext(
    DbContextOptions<MediaDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<CourseReferences.CourseReferenceVersion> CourseReferenceVersions => Set<CourseReferences.CourseReferenceVersion>();
    public DbSet<CourseReferences.CourseVideoReference> CourseVideoReferences => Set<CourseReferences.CourseVideoReference>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<PlaybackSession> PlaybackSessions => Set<PlaybackSession>();

    public DbSet<VideoTag> Tags => Set<VideoTag>();
    public DbSet<VideoTagLink> VideoTags => Set<VideoTagLink>();

    public DbSet<Video> Videos => Set<Video>();

    public DbSet<VideoUpload> VideoUploads => Set<VideoUpload>();

    public DbSet<OperationIdempotencyRecord> OperationIdempotencyRecords => Set<OperationIdempotencyRecord>();

    public DbSet<Transcription> Transcriptions => Set<Transcription>();
    public DbSet<TranscriptionContent> TranscriptionContents => Set<TranscriptionContent>();
    public DbSet<TranscriptionEvent> TranscriptionEvents => Set<TranscriptionEvent>();
    public DbSet<TranscriptionAudioCleanup> TranscriptionAudioCleanups => Set<TranscriptionAudioCleanup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(MediaSchema.Name);
        modelBuilder.Entity<Transcription>().HasQueryFilter(row => tenantContext.TenantId.HasValue && row.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<TranscriptionContent>().HasQueryFilter(row => tenantContext.TenantId.HasValue && row.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<TranscriptionEvent>().HasQueryFilter(row => tenantContext.TenantId.HasValue && row.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<TranscriptionAudioCleanup>().HasQueryFilter(row => tenantContext.TenantId.HasValue && row.TenantId == tenantContext.TenantId.Value);

        modelBuilder.Entity<PlaybackSession>().HasQueryFilter(session => tenantContext.TenantId.HasValue && session.TenantId == tenantContext.TenantId.Value);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
        modelBuilder.Entity<CourseReferences.CourseReferenceVersion>().HasQueryFilter(reference => tenantContext.TenantId.HasValue && reference.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<CourseReferences.CourseVideoReference>().HasQueryFilter(reference => tenantContext.TenantId.HasValue && reference.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VideoTag>().HasQueryFilter(tag => tenantContext.TenantId.HasValue && tag.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VideoTagLink>().HasQueryFilter(link => tenantContext.TenantId.HasValue && link.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Video>().HasQueryFilter(
            video => tenantContext.TenantId.HasValue && video.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VideoUpload>().HasQueryFilter(
            upload => tenantContext.TenantId.HasValue && upload.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<OperationIdempotencyRecord>().HasQueryFilter(
            record => tenantContext.TenantId.HasValue && record.TenantId == tenantContext.TenantId.Value);
    }
}
