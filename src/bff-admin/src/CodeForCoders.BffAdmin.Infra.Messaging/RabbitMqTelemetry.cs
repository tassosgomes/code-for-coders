using System.Diagnostics;

namespace CodeForCoders.BffAdmin.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.BffAdmin.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
