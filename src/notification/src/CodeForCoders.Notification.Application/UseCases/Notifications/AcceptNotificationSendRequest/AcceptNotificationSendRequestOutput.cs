using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;

public sealed record AcceptNotificationSendRequestOutput(
    Guid DeliveryRecordId,
    Guid RequestId,
    Guid TenantId,
    DeliveryStatus Status,
    DateTimeOffset? AcceptedOn,
    DateTimeOffset? RefusedOn,
    string? Reason);
