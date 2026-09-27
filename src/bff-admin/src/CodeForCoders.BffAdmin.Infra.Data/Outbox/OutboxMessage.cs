using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Infra.Data.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string? DestinationExchange { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string? PayloadKeyVersion { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public DateTimeOffset? ProcessedOn { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresOn { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public string? TraceParent { get; private set; }

    public static OutboxMessage Create(OutboxMessageDraft draft, string payload, string? payloadKeyVersion)
        => new()
        {
            Id = draft.Id,
            TenantId = draft.TenantId,
            Type = draft.Type,
            RoutingKey = draft.RoutingKey,
            DestinationExchange = draft.DestinationExchange,
            Payload = payload,
            PayloadKeyVersion = payloadKeyVersion,
            OccurredOn = draft.OccurredOn,
            TraceParent = draft.TraceParent,
        };

    public void AcquireLease(Guid leaseToken, DateTimeOffset leaseExpiresOn)
    {
        LeaseToken = leaseToken;
        LeaseExpiresOn = leaseExpiresOn;
    }

    public void MarkProcessed(DateTimeOffset processedOn)
    {
        ProcessedOn = processedOn;
        LeaseToken = null;
        LeaseExpiresOn = null;
    }

    public void RegisterFailure(string errorCode)
    {
        Attempts++;
        LastError = errorCode.Length <= 64 ? errorCode : errorCode[..64];
        LeaseToken = null;
        LeaseExpiresOn = null;
    }
}
