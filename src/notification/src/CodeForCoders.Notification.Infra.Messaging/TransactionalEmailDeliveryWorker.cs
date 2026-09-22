using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.Repositories;
using CodeForCoders.Notification.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

namespace CodeForCoders.Notification.Infra.Messaging;

public sealed class TransactionalEmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryOptions> options,
    ILogger<TransactionalEmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.GetPollingInterval());
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await DeliverOneAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (NpgsqlException exception)
                {
                    logger.LogWarning(exception, "Notification delivery polling failed because PostgreSQL is unavailable.");
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "Transactional email delivery failed and will be retried.");
                }
                catch (DbUpdateException exception)
                {
                    logger.LogWarning(exception, "Notification delivery state could not be committed; the worker will continue.");
                }
                catch (ExecutionRejectedException exception)
                {
                    logger.LogWarning(exception, "Transactional email delivery was rejected by the resilience pipeline; the worker will continue.");
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogWarning(exception, "Notification delivery worker is not configured correctly.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeliveryRecordRepository>();

        // The claim is a single atomic UPDATE ... RETURNING statement (see
        // DeliveryRecordRepository.ClaimNextAcceptedAsync); no explicit transaction is opened here,
        // so the call to the email provider below never runs inside one (baseline G17).
        var now = DateTimeOffset.UtcNow;
        var claimed = await repository.ClaimNextAcceptedAsync(
            now,
            now.Add(options.Value.GetClaimLease()),
            cancellationToken);
        if (claimed is null)
        {
            return;
        }

        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(claimed.TenantId);
        var handler = scope.ServiceProvider.GetRequiredService<ITransactionalEmailDeliveryMessageHandler>();
        await handler.HandleAsync(claimed.Id, cancellationToken);
    }
}
