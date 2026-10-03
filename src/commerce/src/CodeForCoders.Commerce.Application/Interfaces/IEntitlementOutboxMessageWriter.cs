namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IEntitlementOutboxMessageWriter
{
    Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken);
}
