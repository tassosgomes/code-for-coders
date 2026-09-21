using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Domain.DeliveryRecords;

public sealed class DeliveryRecord
{
    public const int RecipientMaxLength = 320;
    public const int RecipientNameMaxLength = 255;
    public const int PurposeMaxLength = 100;
    public const int ModelMaxLength = 100;
    public const int LinkMaxLength = 2048;
    public const int CorrelationIdMaxLength = 200;

    private DeliveryRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid RequestId { get; private set; }

    public string Recipient { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string Link { get; private set; } = string.Empty;

    public string Purpose { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string? CorrelationId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public DateTimeOffset RequestedOn { get; private set; }

    public DateTimeOffset AcceptedOn { get; private set; }

    public DateTimeOffset? DeliveredOn { get; private set; }

    public static DeliveryRecord Create(
        Guid tenantId,
        Guid requestId,
        string recipient,
        string recipientName,
        string link,
        string purpose,
        string model,
        DateTimeOffset requestedOn,
        DateTimeOffset acceptedOn,
        string? correlationId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Tenant id must not be empty.");
        }

        if (requestId == Guid.Empty)
        {
            throw new EntityValidationException("Request id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new EntityValidationException("Recipient must not be empty.");
        }

        if (recipient.Length > RecipientMaxLength)
        {
            throw new EntityValidationException("Recipient is too long.");
        }

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new EntityValidationException("Recipient name must not be empty.");
        }

        if (recipientName.Length > RecipientNameMaxLength)
        {
            throw new EntityValidationException("Recipient name is too long.");
        }

        if (string.IsNullOrWhiteSpace(link))
        {
            throw new EntityValidationException("Link must not be empty.");
        }

        if (link.Length > LinkMaxLength)
        {
            throw new EntityValidationException("Link is too long.");
        }

        if (string.IsNullOrWhiteSpace(purpose) || purpose.Length > PurposeMaxLength)
        {
            throw new EntityValidationException("Purpose is invalid.");
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length > ModelMaxLength)
        {
            throw new EntityValidationException("Model is invalid.");
        }

        if (correlationId is not null && correlationId.Length > CorrelationIdMaxLength)
        {
            throw new EntityValidationException("Correlation id is too long.");
        }

        return new DeliveryRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            RequestId = requestId,
            Recipient = recipient,
            RecipientName = recipientName,
            Link = link,
            Purpose = purpose,
            Model = model,
            CorrelationId = correlationId,
            Status = DeliveryStatus.Accepted,
            RequestedOn = requestedOn,
            AcceptedOn = acceptedOn,
        };
    }

    public void MarkDelivered(DateTimeOffset deliveredOn)
    {
        if (Status != DeliveryStatus.Accepted)
        {
            throw new EntityValidationException("Only an accepted delivery can be marked as delivered.");
        }

        Status = DeliveryStatus.Delivered;
        DeliveredOn = deliveredOn;
    }
}
