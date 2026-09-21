using System.Diagnostics;

namespace CodeForCoders.Audit.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Audit.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
