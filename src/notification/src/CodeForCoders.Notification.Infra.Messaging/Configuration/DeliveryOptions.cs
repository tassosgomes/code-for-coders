using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Messaging.Configuration;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 1;

    [Range(0, 60000)]
    public int PollingIntervalMilliseconds { get; set; }

    [Range(1, 60000)]
    public int InitialBackoffMilliseconds { get; set; } = 1000;

    [Range(1, 10)]
    public double BackoffMultiplier { get; set; } = 2;

    [Range(1, 86400000)]
    public int MaximumBackoffMilliseconds { get; set; } = 60000;

    /// <summary>
    /// How long a claimed record stays invisible to other pollers while it is being delivered
    /// outside of any transaction. Must comfortably exceed the provider's total request timeout
    /// (20s, see <c>ServiceConfigurationExtensions.AddNotificationConfiguration</c>) so a slow
    /// but in-flight attempt is never reclaimed by a concurrent worker instance.
    /// </summary>
    [Range(1, 3600)]
    public int ClaimLeaseSeconds { get; set; } = 30;

    public TimeSpan GetPollingInterval()
        => PollingIntervalMilliseconds > 0
            ? TimeSpan.FromMilliseconds(PollingIntervalMilliseconds)
            : TimeSpan.FromSeconds(PollingIntervalSeconds);

    public TimeSpan GetClaimLease()
        => TimeSpan.FromSeconds(ClaimLeaseSeconds);
}
