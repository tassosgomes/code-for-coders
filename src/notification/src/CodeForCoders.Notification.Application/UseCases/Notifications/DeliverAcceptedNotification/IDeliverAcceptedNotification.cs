using CodeForCoders.Notification.Application.UseCases;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.DeliverAcceptedNotification;

public interface IDeliverAcceptedNotification
    : IUseCase<DeliverAcceptedNotificationInput, DeliverAcceptedNotificationOutput>;
