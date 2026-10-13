using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

/// <summary>Shared steps for tests that drive the transcription worker against the simulated service.</summary>
internal static class TranscriptionTestHarness
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<VideoPreparationTestContext> CreateContextAsync(VideoLibraryApiFactory factory, TranscriptionHttpSimulator server, bool includeAudio = true)
        => VideoPreparationTestContext.CreateAsync(factory, Token, new(IncludeAudio: includeAudio, TranscriptionServiceAddress: server.BaseAddress, TranscriptionEnabled: true));

    public static async Task<(VideoPreparationTestContext.TestVideo Video, Guid Id)> EnqueuePreparedAsync(VideoPreparationTestContext context)
    {
        var video = await context.CreateQueuedVideoAsync(640, 480, 1, Token);
        Assert.True(await context.ExecuteNextAsync(long.MaxValue / 4, Token));
        var id = await context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>()
            .Transcriptions.IgnoreQueryFilters().Where(row => row.VideoId == video.VideoId)
            .Select(row => row.TranscriptionId).SingleAsync(Token));
        return (video, id);
    }

    public static Task<bool> ExecuteAsync(VideoPreparationTestContext context, long disk = long.MaxValue / 4)
        => context.WithScopeAsync(provider => provider.GetRequiredService<ITranscriptionWorkflow>().ExecuteNextAsync(new(context.WorkDirectory, disk), Token));

    public static async Task<bool> RetryAsync(VideoPreparationTestContext context, Guid id)
    {
        await MakeEligibleAsync(context, id);
        return await ExecuteAsync(context);
    }

    public static Task<Transcription> AttemptAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().Transcriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.TranscriptionId == id, Token));

    public static Task<Transcription?> FindAttemptAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().Transcriptions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(row => row.TranscriptionId == id, Token));

    public static Task<TranscriptionContent?> ContentAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().TranscriptionContents.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(row => row.TranscriptionId == id, Token));

    public static Task<bool> MakeEligibleAsync(VideoPreparationTestContext context, Guid id)
        => context.WithScopeAsync(async provider => { await provider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET next_action_at = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE transcription_id = {id}", Token); return true; });

    public static string AudioPrefix(VideoPreparationTestContext.TestVideo video) => $"{video.TenantId:D}/{video.VideoId:D}/transcription";

    public static async Task AssertCleanAsync(VideoPreparationTestContext context, VideoPreparationTestContext.TestVideo video)
    {
        Assert.Empty(await context.ListObjectKeysAsync(AudioPrefix(video), Token));
        var root = Path.Combine(context.WorkDirectory, "transcription");
        Assert.True(!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any());
        Assert.Empty(Directory.EnumerateFiles(context.WorkDirectory, "key.bin", SearchOption.AllDirectories));
    }

    /// <summary>The attempt waits for the given delay (with a margin) before the worker may claim it again.</summary>
    public static void AssertScheduledIn(Transcription attempt, TimeSpan delay, DateTimeOffset after)
    {
        Assert.InRange(attempt.NextActionAt, after + delay - TimeSpan.FromSeconds(2), DateTimeOffset.UtcNow + delay + TimeSpan.FromSeconds(2));
    }
}
