using CodeForCoders.Notification.Contracts;

namespace CodeForCoders.Notification.Application.Interfaces;

public interface INotificationSendRequestMessageHandler
{
    Task HandleAsync(
        NotificationSendRequestedV1 request,
        string? correlationId,
        CancellationToken cancellationToken);
}
