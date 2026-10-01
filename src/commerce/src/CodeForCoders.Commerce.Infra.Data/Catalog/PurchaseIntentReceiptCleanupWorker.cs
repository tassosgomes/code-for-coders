using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class PurchaseIntentReceiptCleanupWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<PurchaseIntentReceiptCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
                var now = timeProvider.GetUtcNow();
                // Expiration is also enforced atomically by registration; this only bounds retained storage.
                await dbContext.PurchaseIntentReceipts.IgnoreQueryFilters().Where(receipt => receipt.ExpiresAt <= now)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (System.Data.Common.DbException exception)
            {
                logger.LogWarning(exception, "Purchase intent receipt cleanup could not reach the database.");
            }
        }
    }
}
