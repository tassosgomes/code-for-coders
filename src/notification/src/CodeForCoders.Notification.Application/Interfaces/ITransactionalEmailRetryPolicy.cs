namespace CodeForCoders.Notification.Application.Interfaces;

public interface ITransactionalEmailRetryPolicy
{
    int MaxAttempts { get; }

    TimeSpan GetBackoff(int attemptNumber);
}
