using System.Diagnostics.CodeAnalysis;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class VideoPreparationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<VideoPreparationOptions> options,
    TimeProvider timeProvider,
    ILogger<VideoPreparationWorker> logger) : BackgroundService
{
    private const int MaintenanceBatchSize = 100;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected worker failures are logged so later cycles can continue.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollingIntervalSeconds), timeProvider);
        do
        {
            try
            {
                await ExecuteCycleAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The video preparation worker cycle failed unexpectedly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecuteCycleAsync(VideoPreparationOptions settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(settings.WorkDirectory);
        SetPrivateDirectoryMode(settings.WorkDirectory);
        await RunMaintenanceAsync(
            "expired preparation lease recovery",
            workflow => workflow.RecoverExpiredLeasesAsync(MaintenanceBatchSize, cancellationToken),
            cancellationToken);
        await RunMaintenanceAsync(
            "original object cleanup",
            workflow => workflow.CleanupReadyOriginalsAsync(MaintenanceBatchSize, cancellationToken),
            cancellationToken);

        var diskBudget = new DriveInfo(settings.WorkDirectory).AvailableFreeSpace / settings.MaxConcurrency;
        while (!cancellationToken.IsCancellationRequested)
        {
            var workers = Enumerable.Range(0, settings.MaxConcurrency)
                .Select(_ => ExecuteOneAsync(settings, diskBudget, cancellationToken));
            var processed = await Task.WhenAll(workers);
            if (!processed.Any(value => value))
            {
                return;
            }

            diskBudget = new DriveInfo(settings.WorkDirectory).AvailableFreeSpace / settings.MaxConcurrency;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Each maintenance scan is isolated so another scan still runs in this cycle.")]
    private async Task RunMaintenanceAsync(
        string operation,
        Func<IVideoPreparationWorkflow, Task<int>> execute,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IVideoPreparationWorkflow>();
            var count = await execute(workflow);
            if (count > 0)
            {
                logger.LogInformation("Completed {Count} items in the media {Operation} scan.", count, operation);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The media {Operation} scan failed; it will run again next cycle.", operation);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed item is logged and left for lease recovery instead of terminating the worker host.")]
    private async Task<bool> ExecuteOneAsync(
        VideoPreparationOptions settings,
        long diskBudget,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IVideoPreparationWorkflow>();
            return await workflow.ExecuteNextAsync(
                settings.WorkDirectory,
                diskBudget,
                TimeSpan.FromSeconds(settings.LeaseDurationSeconds),
                TimeSpan.FromSeconds(settings.LeaseRenewalIntervalSeconds),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A video preparation failed; the worker will continue with the next item.");
            return false;
        }
    }

    private static void SetPrivateDirectoryMode(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
