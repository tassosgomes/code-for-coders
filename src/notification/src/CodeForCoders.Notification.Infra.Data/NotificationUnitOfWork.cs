using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.Infra.Data;

public sealed class NotificationUnitOfWork(NotificationDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
