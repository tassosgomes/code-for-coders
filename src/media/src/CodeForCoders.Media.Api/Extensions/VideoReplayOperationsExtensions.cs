using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Api.Extensions;

public static class VideoReplayOperationsExtensions
{
    public static async Task RunWithVideoReplayOperationsAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<OutboxOptions>>().Value;
        if (options.VideoFactReplayTenantId is null && options.ProgressFactReplayTenantId is null)
        {
            await app.RunAsync();
            return;
        }

        var topology = ActivatorUtilities.CreateInstance<RabbitMqTopologyInitializer>(app.Services);
        await topology.StartAsync(CancellationToken.None);
        using var worker = ActivatorUtilities.CreateInstance<OutboxPublisherWorker>(app.Services);

        if (options.VideoFactReplayTenantId is { } videoTenantId)
        {
            var count = await worker.ReplayVideoFactsAsync(videoTenantId, CancellationToken.None);
            app.Logger.LogInformation("Video fact replay completed: {MessageCount} retained facts. Reconcile Learning before enabling course authoring.", count);
        }

        if (options.ProgressFactReplayTenantId is { } progressTenantId)
        {
            var count = await worker.ReplayProgressFactsAsync(progressTenantId, CancellationToken.None);
            app.Logger.LogInformation("Progress fact replay completed: {MessageCount} retained facts.", count);
        }
    }
}
