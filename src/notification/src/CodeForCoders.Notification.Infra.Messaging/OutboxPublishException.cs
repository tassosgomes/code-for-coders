namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class OutboxPublishException(string message, Exception innerException)
    : Exception(message, innerException);
