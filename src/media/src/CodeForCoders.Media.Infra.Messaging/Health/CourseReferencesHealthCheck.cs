using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

namespace CodeForCoders.Media.Infra.Messaging.Health;

public sealed class CourseReferencesHealthCheck(RabbitMqConnectionProvider connection, IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken);
            var deadLetters = await channel.QueueDeclarePassiveAsync(options.Value.CoursePublicationsQueue + ".dlq", cancellationToken);
            var source = await channel.QueueDeclarePassiveAsync(options.Value.CoursePublicationsQueue, cancellationToken);
            if (deadLetters.MessageCount > 0) return HealthCheckResult.Unhealthy("Course references require dead-letter recovery.");
            return source.ConsumerCount == 0 ? HealthCheckResult.Unhealthy("Course reference projection has no consumer.") : HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is BrokerUnreachableException or OperationInterruptedException or AlreadyClosedException)
        {
            return HealthCheckResult.Unhealthy("Course reference projection queue is unavailable.", exception);
        }
    }
}
