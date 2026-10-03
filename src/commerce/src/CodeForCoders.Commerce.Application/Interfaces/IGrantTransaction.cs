namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IGrantTransaction : IAsyncDisposable
{
    Task CompleteAsync(CancellationToken cancellationToken);
}
