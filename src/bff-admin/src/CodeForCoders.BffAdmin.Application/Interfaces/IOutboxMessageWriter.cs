namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface IOutboxMessageWriter
{
    Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken);
}

public sealed record OutboxMessageDraft(
    Guid Id,
    Guid TenantId,
    string Type,
    string RoutingKey,
    object Payload,
    DateTimeOffset OccurredOn,
    string? TraceParent);
