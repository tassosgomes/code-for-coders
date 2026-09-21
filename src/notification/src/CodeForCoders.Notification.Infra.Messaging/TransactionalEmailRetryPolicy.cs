using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class TransactionalEmailRetryPolicy(
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<DeliveryOptions> deliveryOptions) : ITransactionalEmailRetryPolicy
{
    public int MaxAttempts => rabbitMqOptions.Value.ProviderDeliveryLimit;

    public TimeSpan GetBackoff(int attemptNumber)
    {
        var settings = deliveryOptions.Value;
        var exponent = Math.Max(0, attemptNumber - 1);
        var milliseconds = settings.InitialBackoffMilliseconds
            * Math.Pow(settings.BackoffMultiplier, exponent);
        var boundedMilliseconds = Math.Min(
            milliseconds,
            settings.MaximumBackoffMilliseconds);
        return TimeSpan.FromMilliseconds(boundedMilliseconds);
    }
}
