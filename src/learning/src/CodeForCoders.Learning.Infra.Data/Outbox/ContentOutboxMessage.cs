using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.Infra.Data.Outbox;

public sealed class ContentOutboxMessage : IOutboxDelivery
{
    private ContentOutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid TenantId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; private set; }

    public DateTimeOffset? ProcessedOn { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public string? TraceParent { get; private set; }

    public static ContentOutboxMessage Create(OutboxMessageDraft draft, string payload)
    {
        return new ContentOutboxMessage
        {
            Id = draft.Id,
            MessageId = draft.Id,
            TenantId = draft.TenantId,
            Type = draft.Type,
            RoutingKey = draft.RoutingKey,
            Payload = payload,
            OccurredOn = draft.OccurredOn,
            TraceParent = draft.TraceParent,
        };
    }

    public static ContentOutboxMessage CreateReplay(OutboxMessageDraft draft, string payload)
    {
        var message = Create(draft, payload);
        message.Id = Guid.CreateVersion7();
        return message;
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
