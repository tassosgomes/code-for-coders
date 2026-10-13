namespace CodeForCoders.Media.Application.Common;

public sealed class TranscriptionOptions
{
    public const string SectionName = "Transcription";
    public bool Enabled { get; set; }
    public int SourceUrlLifetimeMinutes { get; set; } = 90;
    public int MaxWaitHours { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 1;
    public int MaxSubmissions { get; set; } = 3;
    public int MaxSubmissionFailures { get; set; } = 5;
    public int MaxExtractionAttempts { get; set; } = 3;
    // Arrays stay null until configured: the configuration binder appends to a pre-filled array instead of replacing it.
    public int[]? SubmissionRetryMinutes { get; set; }
    public int[]? ResubmissionDelayMinutes { get; set; }
    public int[]? ExtractionRetryMinutes { get; set; }
    public int InitialPollSeconds { get; set; } = 60;
    public int IntermediatePollSeconds { get; set; } = 300;
    public int LongPollSeconds { get; set; } = 900;
    public int PollingIntervalSeconds { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 300;
    public int LeaseRenewalIntervalSeconds { get; set; } = 60;
    public long MinimumFreeDiskBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    public bool HasValidWorkerSettings()
        => SourceUrlLifetimeMinutes is >= 60 and <= 1440 && MaxWaitHours is >= 1 and <= 72
        && MaxConcurrency is >= 1 and <= 8 && MaxSubmissions is >= 1 and <= 10
        && InitialPollSeconds > 0 && IntermediatePollSeconds > 0 && LongPollSeconds > 0
        && PollingIntervalSeconds is >= 1 and <= 300 && LeaseDurationSeconds is >= 30 and <= 3600
        && LeaseRenewalIntervalSeconds >= 5 && LeaseRenewalIntervalSeconds < LeaseDurationSeconds
        && MinimumFreeDiskBytes > 0
        && MaxSubmissionFailures is >= 1 and <= 20 && MaxExtractionAttempts is >= 1 and <= 10
        && ValidDelays(SubmissionRetryMinutes) && ValidDelays(ResubmissionDelayMinutes) && ValidDelays(ExtractionRetryMinutes);

    public TimeSpan SubmissionRetryDelay(int failures) => Delay(SubmissionRetryMinutes ?? [1, 5, 15, 30], failures);
    public TimeSpan ResubmissionDelay(int submissions) => Delay(ResubmissionDelayMinutes ?? [5, 30], submissions);
    public TimeSpan ExtractionRetryDelay(int attempts) => Delay(ExtractionRetryMinutes ?? [1, 5], attempts);

    private static TimeSpan Delay(int[] minutes, int occurrence) => TimeSpan.FromMinutes(minutes[Math.Clamp(occurrence - 1, 0, minutes.Length - 1)]);
    private static bool ValidDelays(int[]? minutes) => minutes is null || (minutes.Length > 0 && minutes.All(value => value is >= 0 and <= 1440));

    public TimeSpan PollDelay(DateTimeOffset submittedAt, DateTimeOffset now)
        => TimeSpan.FromSeconds((now - submittedAt).TotalMinutes < 10 ? InitialPollSeconds
            : (now - submittedAt).TotalHours < 2 ? IntermediatePollSeconds : LongPollSeconds);
}
