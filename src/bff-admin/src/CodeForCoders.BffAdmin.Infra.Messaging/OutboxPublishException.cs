namespace CodeForCoders.BffAdmin.Infra.Messaging;

public sealed class OutboxPublishException(string message, Exception innerException, bool brokerUnavailable = false)
    : Exception(message, innerException)
{
    public bool BrokerUnavailable { get; } = brokerUnavailable;
}
