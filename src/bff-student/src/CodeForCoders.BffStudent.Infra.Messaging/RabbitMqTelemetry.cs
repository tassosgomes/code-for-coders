using System.Diagnostics;

namespace CodeForCoders.BffStudent.Infra.Messaging;

public static class RabbitMqTelemetry
{
    public const string SourceName = "CodeForCoders.BffStudent.RabbitMq";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
