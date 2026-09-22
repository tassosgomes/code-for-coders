namespace CodeForCoders.Notification.Application.Interfaces;

public interface ITransactionalEmailDeliveryMessageHandler
{
    Task HandleAsync(Guid deliveryRecordId, CancellationToken cancellationToken);
}
