namespace CodeForCoders.Commerce.Application.Interfaces;

public interface ICatalogEditTransaction : IAsyncDisposable
{
    Task CompleteAsync(CancellationToken cancellationToken);
}
