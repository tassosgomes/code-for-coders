namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogOutboxMessageWriter
{
    Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken);
}
