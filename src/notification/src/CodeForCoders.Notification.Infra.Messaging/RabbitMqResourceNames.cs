using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Domain.SeedWork;
using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class RabbitMqResourceNames(
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<NotificationProcessingOptions> processingOptions)
{
    public string Exchange => Compose(rabbitOptions.Value.Exchange, processingOptions.Value.Namespace);

    public string DeadLetterExchange
        => Compose(rabbitOptions.Value.DeadLetterExchange, processingOptions.Value.Namespace);

    public string HeartbeatQueue
        => Compose(rabbitOptions.Value.HeartbeatQueue, processingOptions.Value.Namespace);

    public string SendRequestQueue
        => Compose(rabbitOptions.Value.SendRequestQueue, processingOptions.Value.Namespace);

    public string SendRequestRoutingKey => rabbitOptions.Value.SendRequestRoutingKey;

    public int DeliveryLimit => rabbitOptions.Value.DeliveryLimit;

    public static string Compose(string baseName, string processingNamespace)
        => $"{baseName}.{NotificationNamespace.Validate(processingNamespace)}";
}
