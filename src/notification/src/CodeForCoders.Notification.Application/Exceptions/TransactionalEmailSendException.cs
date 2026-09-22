namespace CodeForCoders.Notification.Application.Exceptions;

public sealed class TransactionalEmailSendException(
    string reason,
    bool isTransient,
    Exception? innerException = null) : Exception(reason, innerException)
{
    public string Reason { get; } = reason;

    public bool IsTransient { get; } = isTransient;
}
