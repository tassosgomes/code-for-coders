namespace CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;

public sealed record AcceptNotificationSendRequestOutput(
    Guid DeliveryRecordId,
    Guid RequestId,
    Guid TenantId,
    DateTimeOffset AcceptedOn);
