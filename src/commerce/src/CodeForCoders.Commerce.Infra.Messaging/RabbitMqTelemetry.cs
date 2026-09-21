using System.Diagnostics;

namespace CodeForCoders.Commerce.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Commerce.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
