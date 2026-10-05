namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IOrderTransaction : IAsyncDisposable
{
    Task CompleteAsync(CancellationToken cancellationToken);
}
