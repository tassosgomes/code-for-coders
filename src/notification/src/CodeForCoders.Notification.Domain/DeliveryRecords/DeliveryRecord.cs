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
    public const int ReasonMaxLength = 2000;
    public const int NamespaceMaxLength = NotificationNamespace.MaxLength;

    private DeliveryRecord()
    {
    }

    public Guid Id { get; private set; }

    public string Namespace { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public Guid RequestId { get; private set; }

    public string? Recipient { get; private set; }

    public string? RecipientName { get; private set; }

    public string? RecipientRole { get; private set; }

    public string? Link { get; private set; }

    public string? Purpose { get; private set; }

    public string? Model { get; private set; }

    public string? Reason { get; private set; }

    public string? CorrelationId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public DateTimeOffset RequestedOn { get; private set; }

    public DateTimeOffset? AcceptedOn { get; private set; }

    public DateTimeOffset? RefusedOn { get; private set; }

    public DateTimeOffset? DeliveredOn { get; private set; }

    public DateTimeOffset? FailedOn { get; private set; }

    public int ProviderAttemptCount { get; private set; }

    public DateTimeOffset? LastProviderAttemptOn { get; private set; }

    public DateTimeOffset? NextAttemptOn { get; private set; }

    public bool ExhaustedAttempts { get; private set; }

    public static DeliveryRecord Create(
        string processingNamespace,
        Guid tenantId,
        Guid requestId,
        string recipient,
        string? recipientName,
        string link,
        string purpose,
        string model,
        DateTimeOffset requestedOn,
        DateTimeOffset acceptedOn,
        string? correlationId,
        string? recipientRole = null)
    {
        var normalizedNamespace = NotificationNamespace.Validate(processingNamespace);
        ValidateIdentity(tenantId, requestId);
        ValidateRecipient(recipient);
        ValidateOptionalText(recipientName, RecipientNameMaxLength, "Recipient name");
        ValidateOptionalText(recipientRole, 32, "Recipient role");
        if (string.IsNullOrWhiteSpace(recipientName) && string.IsNullOrWhiteSpace(recipientRole))
        {
            throw new EntityValidationException("A recipient name or role is required.");
        }
        ValidateRequiredText(link, LinkMaxLength, "Link");
        ValidateRequiredText(purpose, PurposeMaxLength, "Purpose");
        ValidateRequiredText(model, ModelMaxLength, "Model");
        ValidateCorrelationId(correlationId);

        return new DeliveryRecord
        {
            Id = Guid.CreateVersion7(),
            Namespace = normalizedNamespace,
            TenantId = tenantId,
            RequestId = requestId,
            Recipient = recipient,
            RecipientName = NullIfWhiteSpace(recipientName),
            RecipientRole = NullIfWhiteSpace(recipientRole),
            Link = link,
            Purpose = purpose,
            Model = model,
            CorrelationId = correlationId,
            Status = DeliveryStatus.Accepted,
            RequestedOn = requestedOn,
            AcceptedOn = acceptedOn,
            NextAttemptOn = acceptedOn,
        };
    }

    public static DeliveryRecord CreateRefused(
        string processingNamespace,
        Guid tenantId,
        Guid requestId,
        string recipient,
        string? recipientName,
        string? link,
        string? purpose,
        string? model,
        string reason,
        DateTimeOffset requestedOn,
        DateTimeOffset refusedOn,
        string? correlationId,
        string? recipientRole = null)
    {
        var normalizedNamespace = NotificationNamespace.Validate(processingNamespace);
        ValidateIdentity(tenantId, requestId);
        ValidateRecipient(recipient);
        ValidateOptionalText(recipientName, RecipientNameMaxLength, "Recipient name");
        ValidateOptionalText(recipientRole, 32, "Recipient role");
        ValidateOptionalText(link, LinkMaxLength, "Link");
        ValidateOptionalText(purpose, PurposeMaxLength, "Purpose");
        ValidateOptionalText(model, ModelMaxLength, "Model");
        ValidateRequiredText(reason, ReasonMaxLength, "Reason");
        ValidateCorrelationId(correlationId);

        return new DeliveryRecord
        {
            Id = Guid.CreateVersion7(),
            Namespace = normalizedNamespace,
            TenantId = tenantId,
            RequestId = requestId,
            Recipient = recipient,
            RecipientName = NullIfWhiteSpace(recipientName),
            RecipientRole = NullIfWhiteSpace(recipientRole),
            Link = null,
            Purpose = NullIfWhiteSpace(purpose),
            Model = NullIfWhiteSpace(model),
            Reason = reason,
            CorrelationId = correlationId,
            Status = DeliveryStatus.Refused,
            RequestedOn = requestedOn,
            RefusedOn = refusedOn,
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
        NextAttemptOn = null;
        Link = null;
    }

    public void RegisterProviderAttempt(DateTimeOffset attemptedOn)
    {
        EnsureAccepted();

        ProviderAttemptCount++;
        LastProviderAttemptOn = attemptedOn;
    }

    public void ScheduleRetry(string reason, DateTimeOffset nextAttemptOn)
    {
        EnsureAccepted();
        ValidateRequiredText(reason, ReasonMaxLength, "Reason");

        Reason = reason;
        NextAttemptOn = nextAttemptOn;
    }

    public void MarkFailed(
        string reason,
        bool exhaustedAttempts,
        DateTimeOffset failedOn)
    {
        EnsureAccepted();
        ValidateRequiredText(reason, ReasonMaxLength, "Reason");

        Status = DeliveryStatus.Failed;
        Reason = reason;
        ExhaustedAttempts = exhaustedAttempts;
        FailedOn = failedOn;
        NextAttemptOn = null;
        Link = null;
    }

    public void PurgePersonalData()
    {
        if (Status == DeliveryStatus.Accepted)
        {
            throw new EntityValidationException("Only a final delivery can have personal data purged.");
        }

        Recipient = null;
        RecipientName = null;
        RecipientRole = null;
        Link = null;
        Reason = null;
    }

    private void EnsureAccepted()
    {
        if (Status != DeliveryStatus.Accepted)
        {
            throw new EntityValidationException("Only an accepted delivery can be changed.");
        }
    }

    private static void ValidateIdentity(Guid tenantId, Guid requestId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new EntityValidationException("Tenant id must not be empty.");
        }

        if (requestId == Guid.Empty)
        {
            throw new EntityValidationException("Request id must not be empty.");
        }
    }

    private static void ValidateRecipient(string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new EntityValidationException("Recipient must not be empty.");
        }

        if (recipient.Length > RecipientMaxLength)
        {
            throw new EntityValidationException("Recipient is too long.");
        }
    }

    private static void ValidateRequiredText(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EntityValidationException($"{fieldName} must not be empty.");
        }

        if (value.Length > maxLength)
        {
            throw new EntityValidationException($"{fieldName} is too long.");
        }
    }

    private static void ValidateOptionalText(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new EntityValidationException($"{fieldName} is too long.");
        }
    }

    private static void ValidateCorrelationId(string? correlationId)
    {
        if (correlationId is not null && correlationId.Length > CorrelationIdMaxLength)
        {
            throw new EntityValidationException("Correlation id is too long.");
        }
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
