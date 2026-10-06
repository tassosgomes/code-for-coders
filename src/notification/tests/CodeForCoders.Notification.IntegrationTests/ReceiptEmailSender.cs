using System.Collections.Concurrent;
using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.IntegrationTests;

internal sealed class ReceiptEmailSender : ITransactionalEmailSender
{
    public ConcurrentQueue<TransactionalEmail> Emails { get; } = new();
    public Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
    {
        Emails.Enqueue(email);
        return Task.CompletedTask;
    }
}
