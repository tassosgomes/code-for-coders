using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class DeliveryRecordRetentionOptions
{
    public const string SectionName = "Retention";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    [Range(1, 86400)]
    public int PollingIntervalSeconds { get; set; } = 3600;

    [Range(0, 60000)]
    public int PollingIntervalMilliseconds { get; set; }

    [Range(1, 5000)]
    public int BatchSize { get; set; } = 500;

    public TimeSpan GetPollingInterval()
        => PollingIntervalMilliseconds > 0
            ? TimeSpan.FromMilliseconds(PollingIntervalMilliseconds)
            : TimeSpan.FromSeconds(PollingIntervalSeconds);
}
