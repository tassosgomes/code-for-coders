using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Messaging.Configuration;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    [Range(1, 60)]
    public int PollingIntervalSeconds { get; set; } = 1;
}
