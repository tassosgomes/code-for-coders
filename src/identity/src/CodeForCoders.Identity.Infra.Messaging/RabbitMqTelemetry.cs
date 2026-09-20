using System.Diagnostics;

namespace CodeForCoders.Identity.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Identity.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
