using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Domain.DeliveryRecords;

public sealed class DeliveryOutcomeCounter
{
    public const string UnspecifiedPurpose = "sem-finalidade";

    private DeliveryOutcomeCounter()
    {
    }

    public Guid TenantId { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public DeliveryStatus Status { get; private set; }

    public DateOnly OutcomeDay { get; private set; }

    public long Count { get; private set; }

    public static DeliveryOutcomeCounter Create(
        Guid tenantId,
        string? purpose,
        DeliveryStatus status,
        DateOnly outcomeDay)
    {
        if (tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Tenant id must not be empty.");
        }

        if (status is not DeliveryStatus.Delivered
            and not DeliveryStatus.Refused
            and not DeliveryStatus.Failed)
        {
            throw new EntityValidationException("Only a final delivery status can be counted.");
        }

        var purposeKey = string.IsNullOrWhiteSpace(purpose)
            ? UnspecifiedPurpose
            : purpose;
        if (purposeKey.Length > DeliveryRecord.PurposeMaxLength)
        {
            throw new EntityValidationException("Purpose is too long.");
        }

        return new DeliveryOutcomeCounter
        {
            TenantId = tenantId,
            Purpose = purposeKey,
            Status = status,
            OutcomeDay = outcomeDay,
            Count = 1,
        };
    }

    public void Increment()
    {
        Count++;
    }
}
