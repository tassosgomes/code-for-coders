namespace CodeForCoders.Identity.Application.Common;

public sealed class OutboxDestinationOptions
{
    public const string SectionName = "RabbitMq";

    public string Exchange { get; set; } = "identity.events";

    public string NotificationExchange { get; set; } = "notification.events.default";
}
