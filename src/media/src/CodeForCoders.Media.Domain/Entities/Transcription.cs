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

    public void MarkSubmitted(string jobId, DateTimeOffset now, TimeSpan maxWait, TimeSpan pollDelay)
    {
        if (Status != "working" || string.IsNullOrWhiteSpace(jobId) || jobId.Length > 128)
            throw new EntityValidationException("The transcription submission transition is invalid.");
        WhisperJobId = jobId;
        Submissions++;
        SubmittedAt = now;
        DeadlineAt = now.Add(maxWait);
        SchedulePoll(now.Add(pollDelay));
    }

    public void SchedulePoll(DateTimeOffset nextActionAt)
    {
        if (Status != "working") throw new EntityValidationException("The transcription poll transition is invalid.");
        Status = "waiting";
        NextActionAt = SignaledAt is { } signal && signal < nextActionAt ? signal : nextActionAt;
        LeaseId = null;
        LeaseUntil = null;
    }

    public void Signal(string jobId, DateTimeOffset now)
    {
        if (Status is not ("queued" or "working" or "waiting")
            || (WhisperJobId is not null && WhisperJobId != jobId)) return;
        SignaledAt = now;
        NextActionAt = now;
    }

    public void MarkReady(TranscriptionContent content, DateTimeOffset now)
    {
        if (Status != "working" || content.TranscriptionId != TranscriptionId || content.TenantId != TenantId)
            throw new EntityValidationException("The transcription completion transition is invalid.");
        Status = "ready";
        IsCurrent = true;
        ReadyAt = now;
        DurationMs = content.DurationMs;
        using var document = System.Text.Json.JsonDocument.Parse(content.SegmentsJson);
        SegmentCount = document.RootElement.GetArrayLength();
        LeaseId = null;
        LeaseUntil = null;
    }

    public void Retire() => IsCurrent = false;
    public void MarkAudioDeleted(DateTimeOffset now) => AudioDeletedAt = now;

    public void RecoverExpiredLease(DateTimeOffset now)
    {
        if (Status != "working" || LeaseUntil > now)
            throw new EntityValidationException("The transcription lease has not expired.");
        Status = WhisperJobId is null ? "queued" : "waiting";
        NextActionAt = now;
        LeaseId = null;
        LeaseUntil = null;
    }

    /// <summary>The audio extraction failed before a job existed; the attempt waits in the queue for another extraction.</summary>
    public void RetryExtraction(DateTimeOffset nextActionAt)
    {
        if (Status != "working" || WhisperJobId is not null)
            throw new EntityValidationException("The transcription extraction retry transition is invalid.");
        Requeue(nextActionAt);
    }

    /// <summary>The service did not take the audio; extraction counts restart because the audio is already extracted.</summary>
    public void RetrySubmission(DateTimeOffset nextActionAt)
    {
        if (Status != "working" || WhisperJobId is not null)
            throw new EntityValidationException("The transcription submission retry transition is invalid.");
        SubmissionFailures++;
        ExtractionAttempts = 0;
        Requeue(nextActionAt);
    }

    /// <summary>The accepted job is gone or lost its source; a new extraction and submission follow.</summary>
    public void Resubmit(DateTimeOffset nextActionAt)
    {
        if (Status != "working" || WhisperJobId is null)
            throw new EntityValidationException("The transcription resubmission transition is invalid.");
        WhisperJobId = null;
        SubmittedAt = null;
        DeadlineAt = null;
        AudioDeletedAt = null;
        ExtractionAttempts = 0;
        Requeue(nextActionAt);
    }

    private void Requeue(DateTimeOffset nextActionAt)
    {
        Status = "queued";
        NextActionAt = nextActionAt;
        SignaledAt = null;
        LeaseId = null;
        LeaseUntil = null;
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
