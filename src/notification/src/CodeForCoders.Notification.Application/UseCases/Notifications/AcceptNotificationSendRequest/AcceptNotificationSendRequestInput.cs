using CodeForCoders.Notification.Contracts;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;

public sealed record AcceptNotificationSendRequestInput(
    NotificationSendRequestedV1 Request,
    string? CorrelationId);
