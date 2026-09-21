using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Infra.Data;

internal readonly record struct PendingDeliveryOutcomeCounterIncrement(
    Guid TenantId,
    string Purpose,
    DeliveryStatus Status,
    DateOnly OutcomeDay);
