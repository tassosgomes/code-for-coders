using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Domain.SeedWork;
using CodeForCoders.Billing.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Billing.Infra.Messaging;

public sealed class RabbitMqResourceNames(
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<BillingProcessingOptions> processingOptions)
{
    public string Exchange => Compose(rabbitOptions.Value.Exchange, processingOptions.Value.Namespace);

    public string DeadLetterExchange
        => Compose(rabbitOptions.Value.DeadLetterExchange, processingOptions.Value.Namespace);

    public string HeartbeatQueue
        => Compose(rabbitOptions.Value.HeartbeatQueue, processingOptions.Value.Namespace);

    public string CommerceExchange
        => Compose(rabbitOptions.Value.CommerceExchange, processingOptions.Value.Namespace);

    public string OrderCancellationsQueue
        => Compose(rabbitOptions.Value.OrderCancellationsQueue, processingOptions.Value.Namespace);

    public int DeliveryLimit => rabbitOptions.Value.DeliveryLimit;

    public static string Compose(string baseName, string processingNamespace)
        => $"{baseName}.{BillingNamespace.Validate(processingNamespace)}";
}
