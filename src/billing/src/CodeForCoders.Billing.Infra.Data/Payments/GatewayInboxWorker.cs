using CodeForCoders.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class GatewayInboxWorker(IServiceScopeFactory scopes, ILogger<GatewayInboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            try { await scope.ServiceProvider.GetRequiredService<IPaymentStore>().ProcessPendingAsync(stoppingToken); }
            catch (Exception error) when (error is NpgsqlException or DbUpdateException)
            { logger.LogWarning("Gateway inbox processing temporarily unavailable."); }
        }
    }
}
