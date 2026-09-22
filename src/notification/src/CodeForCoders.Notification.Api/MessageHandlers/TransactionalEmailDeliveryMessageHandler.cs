using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.UseCases.Notifications.DeliverAcceptedNotification;

namespace CodeForCoders.Notification.Api.MessageHandlers;

public sealed class TransactionalEmailDeliveryMessageHandler(
    IDeliverAcceptedNotification deliverAcceptedNotification)
    : ITransactionalEmailDeliveryMessageHandler
{
    public async Task HandleAsync(
        Guid deliveryRecordId,
        CancellationToken cancellationToken)
    {
        await deliverAcceptedNotification.ExecuteAsync(
            new DeliverAcceptedNotificationInput(deliveryRecordId),
            cancellationToken);
    }
}
