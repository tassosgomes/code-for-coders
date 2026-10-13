using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionConfiguration : IEntityTypeConfiguration<Transcription>
{
    public void Configure(EntityTypeBuilder<Transcription> builder)
    {
        builder.ToTable("video_transcriptions");
        builder.HasKey(attempt => attempt.TranscriptionId);
        builder.Property(attempt => attempt.TranscriptionId).HasColumnName("transcription_id").ValueGeneratedNever();
        builder.Property(attempt => attempt.TenantId).HasColumnName("tenant_id");
        builder.Property(attempt => attempt.VideoId).HasColumnName("video_id");
        builder.Property(attempt => attempt.Origin).HasColumnName("origin").HasMaxLength(32);
        builder.Property(attempt => attempt.RequestedByAccountId).HasColumnName("requested_by_account_id");
        builder.Property(attempt => attempt.Status).HasColumnName("status").HasMaxLength(32);
        builder.Property(attempt => attempt.IsCurrent).HasColumnName("is_current");
        builder.Property(attempt => attempt.FailureReason).HasColumnName("failure_reason").HasMaxLength(32);
        builder.Property(attempt => attempt.ExtractionAttempts).HasColumnName("extraction_attempts");
        builder.Property(attempt => attempt.Submissions).HasColumnName("submissions");
        builder.Property(attempt => attempt.SubmissionFailures).HasColumnName("submission_failures");
        builder.Property(attempt => attempt.WhisperJobId).HasColumnName("whisper_job_id").HasMaxLength(128);
        builder.Property(attempt => attempt.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(attempt => attempt.DeadlineAt).HasColumnName("deadline_at");
        builder.Property(attempt => attempt.NextActionAt).HasColumnName("next_action_at");
        builder.Property(attempt => attempt.SignaledAt).HasColumnName("signaled_at");
        builder.Property(attempt => attempt.LeaseId).HasColumnName("lease_id");
        builder.Property(attempt => attempt.LeaseUntil).HasColumnName("lease_until");
        builder.Property(attempt => attempt.RequestedAt).HasColumnName("requested_at");
        builder.Property(attempt => attempt.ReadyAt).HasColumnName("ready_at");
        builder.Property(attempt => attempt.DurationMs).HasColumnName("duration_ms");
        builder.Property(attempt => attempt.SegmentCount).HasColumnName("segment_count");
        builder.Property(attempt => attempt.AudioDeletedAt).HasColumnName("audio_deleted_at");
        builder.HasOne<Video>().WithMany().HasForeignKey(attempt => attempt.VideoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(attempt => new { attempt.TenantId, attempt.VideoId }, "ongoing")
            .IsUnique().HasDatabaseName("ux_video_transcriptions_ongoing").HasFilter("status IN ('queued', 'working', 'waiting')");
        builder.HasIndex(attempt => new { attempt.TenantId, attempt.VideoId }, "current")
            .IsUnique().HasDatabaseName("ux_video_transcriptions_current").HasFilter("is_current");
        builder.HasIndex(attempt => new { attempt.Status, attempt.NextActionAt }).HasDatabaseName("ix_video_transcriptions_claim");
    }
}
