namespace CodeForCoders.Media.Application.Interfaces;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task<bool> TryCommitAsync(CancellationToken cancellationToken);
}
