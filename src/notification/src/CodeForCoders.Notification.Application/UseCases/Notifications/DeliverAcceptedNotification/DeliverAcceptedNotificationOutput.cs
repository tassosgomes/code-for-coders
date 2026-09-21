namespace CodeForCoders.Notification.Application.UseCases.Notifications.DeliverAcceptedNotification;

public sealed record DeliverAcceptedNotificationOutput(
    bool Delivered,
    Guid? EventId,
    DateTimeOffset? DeliveredOn);
