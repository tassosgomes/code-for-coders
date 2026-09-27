using System.Diagnostics.CodeAnalysis;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class ExpiredVideoUploadWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredVideoUploadWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 100;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected scan failures are logged so the worker can retry on its next cycle.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval, timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expireUploads = scope.ServiceProvider.GetRequiredService<IExpirePendingVideoUploads>();
                var count = await expireUploads.ExecuteAsync(BatchSize, stoppingToken);
                if (count > 0)
                {
                    logger.LogInformation("Expired {Count} abandoned video uploads.", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
                when (exception is StorageUnavailableException or DbUpdateException or NpgsqlException)
            {
                logger.LogError(exception, "The abandoned video upload scan failed.");
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The abandoned video upload scan failed unexpectedly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
