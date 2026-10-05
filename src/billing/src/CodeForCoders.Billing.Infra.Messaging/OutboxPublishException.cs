namespace CodeForCoders.Billing.Infra.Messaging;

public sealed class OutboxPublishException(string message, Exception innerException)
    : Exception(message, innerException);
