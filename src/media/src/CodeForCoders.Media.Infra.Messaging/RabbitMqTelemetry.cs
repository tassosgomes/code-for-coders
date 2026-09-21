using System.Diagnostics;

namespace CodeForCoders.Media.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Media.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
