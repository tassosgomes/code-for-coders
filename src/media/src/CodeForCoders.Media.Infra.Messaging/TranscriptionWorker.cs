using System.Diagnostics.CodeAnalysis;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class TranscriptionWorker(
    IServiceScopeFactory scopes,
    IOptions<TranscriptionOptions> options,
    IOptions<VideoPreparationOptions> preparation,
    TimeProvider clock,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cycles are isolated and upstream exception payloads never enter logs.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds), clock);
        do
        {
            try { await ExecuteCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("The transcription worker cycle failed; maintenance will run on the next cycle."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        var root = preparation.Value.WorkDirectory;
        Directory.CreateDirectory(root);
        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ITranscriptionWorkflow>().MaintainAsync(root, cancellationToken);
        var disk = new DriveInfo(root).AvailableFreeSpace / options.Value.MaxConcurrency;
        await Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrency).Select(async _ =>
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ITranscriptionWorkflow>().ExecuteNextAsync(new(root, disk), cancellationToken);
        }));
    }
}
