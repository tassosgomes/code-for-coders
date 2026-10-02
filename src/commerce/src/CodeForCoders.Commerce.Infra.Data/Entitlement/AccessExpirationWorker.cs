using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class AccessExpirationWorker(IServiceScopeFactory scopeFactory, TimeProvider clock,
    IOptions<AccessExpirationOptions> options, ILogger<AccessExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<AccessExpirationCycle>().RunAsync(stoppingToken);
                }
                catch (System.Data.Common.DbException exception)
                {
                    logger.LogWarning(exception, "Access expiration polling could not reach the database.");
                }
                catch (DbUpdateException exception)
                {
                    logger.LogWarning(exception, "Access expiration facts could not be saved and will be retried.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
