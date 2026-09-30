using System.Text.Json;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.VideoProjection;

namespace CodeForCoders.Learning.Api.Extensions;

public static class VideoProjectionOperationsExtensions
{
    public static async Task RunWithVideoProjectionOperationsAsync(this WebApplication app)
    {
        var path = app.Configuration["VideoProjection:ReconcileManifest"];
        if (string.IsNullOrEmpty(path)) { await app.RunAsync(); return; }
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;
        var tenantId = root.GetProperty("tenantId").GetGuid();
        var ids = root.GetProperty("readyVideoIds").EnumerateArray().Select(value => value.GetGuid()).ToHashSet();
        await using var scope = app.Services.CreateAsyncScope();
        var reconciler = new VideoProjectionReconciler(scope.ServiceProvider.GetRequiredService<LearningDbContext>());
        if (!await reconciler.MatchesAsync(tenantId, ids, CancellationToken.None))
            throw new InvalidOperationException("Video projection reconciliation failed. Keep course authoring disabled and request retained facts from Media.");
        app.Logger.LogInformation("Video projection reconciled: {ReadyCount} ready IDs. Authoring may be enabled after all tenants reconcile.", ids.Count);
    }
}
