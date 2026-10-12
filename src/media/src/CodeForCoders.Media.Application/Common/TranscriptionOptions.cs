namespace CodeForCoders.Media.Application.Common;

public sealed class TranscriptionOptions
{
    public const string SectionName = "Transcription";
    public bool Enabled { get; set; }
    public int SourceUrlLifetimeMinutes { get; set; } = 90;
    public int MaxWaitHours { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 1;
    public int MaxSubmissions { get; set; } = 3;
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
        && MinimumFreeDiskBytes > 0;

    public TimeSpan PollDelay(DateTimeOffset submittedAt, DateTimeOffset now)
        => TimeSpan.FromSeconds((now - submittedAt).TotalMinutes < 10 ? InitialPollSeconds
            : (now - submittedAt).TotalHours < 2 ? IntermediatePollSeconds : LongPollSeconds);
}
