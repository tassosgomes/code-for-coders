using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;
using CodeForCoders.Notification.Contracts;

namespace CodeForCoders.Notification.Api.MessageHandlers;

public sealed class NotificationSendRequestedMessageHandler(
    IAcceptNotificationSendRequest acceptNotificationSendRequest)
    : INotificationSendRequestMessageHandler
{
    public async Task HandleAsync(
        NotificationSendRequestedV1 request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await acceptNotificationSendRequest.ExecuteAsync(
            new AcceptNotificationSendRequestInput(request, correlationId),
            cancellationToken);
    }
}
