namespace CodeForCoders.BffStudent.Infra.Messaging;

public sealed class OutboxPublishException(string message, Exception innerException)
    : Exception(message, innerException);
