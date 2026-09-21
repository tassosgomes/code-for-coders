using System.Diagnostics;

namespace CodeForCoders.Learning.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.Learning.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
