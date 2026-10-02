using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class GrantReceiptCleanupWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<GrantReceiptCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (System.Data.Common.DbException exception)
            {
                logger.LogWarning(exception, "Grant receipt cleanup could not reach the database.");
            }
        }
    }
    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var now = timeProvider.GetUtcNow();
        // Expiration is also enforced atomically by registration; this only bounds retained storage.
        await dbContext.GrantReceipts.IgnoreQueryFilters().Where(receipt => receipt.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

}
