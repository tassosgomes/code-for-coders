using System.Diagnostics;

namespace CodeForCoders.Notification.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Notification.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
