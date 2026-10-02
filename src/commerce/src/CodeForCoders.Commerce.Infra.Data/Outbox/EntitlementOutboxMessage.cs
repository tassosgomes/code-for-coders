using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public sealed class EntitlementOutboxMessage : IOutboxDelivery
{
    private EntitlementOutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; private set; }

    public DateTimeOffset? ProcessedOn { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public string? TraceParent { get; private set; }

    public static EntitlementOutboxMessage Create(OutboxMessageDraft draft, string payload)
    {
        return new EntitlementOutboxMessage
        {
            Id = draft.Id,
            TenantId = draft.TenantId,
            Type = draft.Type,
            RoutingKey = draft.RoutingKey,
            Payload = payload,
            OccurredOn = draft.OccurredOn,
            TraceParent = draft.TraceParent,
        };
    }

    public void MarkProcessed()
    {
        ProcessedOn = DateTimeOffset.UtcNow;
    }

    public void RegisterFailure(Exception exception)
    {
        Attempts++;
        LastError = exception.GetType().Name;
    }
}
