using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Infra.Data.Outbox;

public sealed class OutboxMessage
{
    public const int NamespaceMaxLength = NotificationNamespace.MaxLength;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Namespace { get; private set; } = string.Empty;

    public Guid TenantId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOn { get; private set; }

    public DateTimeOffset? ProcessedOn { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public string? TraceParent { get; private set; }

    public string? CorrelationId { get; private set; }

    public static OutboxMessage Create(
        OutboxMessageDraft draft,
        string payload,
        string processingNamespace)
    {
        return new OutboxMessage
        {
            Id = draft.Id,
            Namespace = NotificationNamespace.Validate(processingNamespace),
            TenantId = draft.TenantId,
            Type = draft.Type,
            RoutingKey = draft.RoutingKey,
            Payload = payload,
            OccurredOn = draft.OccurredOn,
            TraceParent = draft.TraceParent,
            CorrelationId = draft.CorrelationId,
        };
    }

    public void MarkProcessed()
    {
        ProcessedOn = DateTimeOffset.UtcNow;
    }

    public void RegisterFailure(Exception exception)
    {
        Attempts++;
        LastError = exception.Message.Length <= 2000
            ? exception.Message
            : exception.Message[..2000];
    }
}
