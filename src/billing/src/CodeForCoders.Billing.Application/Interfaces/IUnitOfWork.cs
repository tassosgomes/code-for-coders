namespace CodeForCoders.Billing.Application.Interfaces;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}
