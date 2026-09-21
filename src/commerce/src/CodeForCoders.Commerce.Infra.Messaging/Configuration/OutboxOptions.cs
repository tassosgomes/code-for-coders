using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Commerce.Infra.Messaging.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 2;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;
}
