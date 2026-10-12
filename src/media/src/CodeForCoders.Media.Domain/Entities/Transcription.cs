using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class Transcription
{
    private Transcription() { }

    public Guid TranscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VideoId { get; private set; }
    public string Origin { get; private set; } = string.Empty;
    public Guid? RequestedByAccountId { get; private set; }
    public string Status { get; private set; } = "queued";
    public bool IsCurrent { get; private set; }
    public string? FailureReason { get; private set; }
    public int ExtractionAttempts { get; private set; }
    public int Submissions { get; private set; }
    public int SubmissionFailures { get; private set; }
    public string? WhisperJobId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? DeadlineAt { get; private set; }
    public DateTimeOffset NextActionAt { get; private set; }
    public DateTimeOffset? SignaledAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public long? DurationMs { get; private set; }
    public int? SegmentCount { get; private set; }
    public DateTimeOffset? AudioDeletedAt { get; private set; }

    public static Transcription Create(TranscriptionCreateInput input)
    {
        if (input.TenantId == Guid.Empty || input.VideoId == Guid.Empty
            || input.Origin is not ("requested" or "automatic")
            || (input.Origin == "requested" && (input.RequestedByAccountId is null || input.RequestedByAccountId == Guid.Empty))
            || (input.Origin == "automatic" && input.RequestedByAccountId is not null))
            throw new EntityValidationException("Transcription request metadata is invalid.");
        return new Transcription
        {
            TranscriptionId = Guid.CreateVersion7(input.RequestedAt),
            TenantId = input.TenantId,
            VideoId = input.VideoId,
            Origin = input.Origin,
            RequestedByAccountId = input.RequestedByAccountId,
            RequestedAt = input.RequestedAt,
            NextActionAt = input.RequestedAt,
        };
    }

    public void MarkFailed(string reason)
    {
        if (Status is not ("queued" or "working" or "waiting")
            || reason is not ("duration-limit" or "size-limit" or "service-unavailable" or "processing-failed" or "timed-out"))
            throw new EntityValidationException("The transcription failure transition is invalid.");
        Status = "failed";
        FailureReason = reason;
        LeaseId = null;
        LeaseUntil = null;
    }
}
