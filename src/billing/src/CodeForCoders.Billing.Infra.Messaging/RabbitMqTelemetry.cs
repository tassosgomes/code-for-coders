using System.Diagnostics;

namespace CodeForCoders.Billing.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Billing.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
