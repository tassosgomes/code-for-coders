using CodeForCoders.Media.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class ExpiredPlaybackSessionWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ExpiredPlaybackSessionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPlaybackSessionMaintenance>()
                    .DeleteExpiredAsync(clock.GetUtcNow().AddHours(-24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (NpgsqlException exception) { logger.LogError(exception, "Playback session retention failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
