using System.Diagnostics.CodeAnalysis;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Videos.GetVideoTranscription;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class TranscriptionRealServiceSmokeTests
{
    [TranscriptionRealServiceSmokeFact]
    [Trait("Category", "RealServiceSmoke")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Infrastructure exception messages may expose secrets, signed URLs or transcript text.")]
    public async Task RealShortLessonIsPreparedTranscribedReadableAndAudioIsRemoved()
    {
        // Missing configuration fails before any infrastructure side effects, even in an explicit run.
        var settings = TranscriptionRealServiceSmokeSettings.Load();
        using var timeout = new CancellationTokenSource(settings.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, timeout.Token);
        var token = linked.Token;
        try
        {
            var tools = await FfmpegTools.EnsureAvailableAsync(token);
            await using var context = new TranscriptionRealServiceSmokeContext(settings, tools);
            await context.InitializeAsync(token);
            await context.UploadAsync(settings.VideoPath, token);
            Assert.True(await context.WithScopeAsync(provider => provider.GetRequiredService<IVideoPreparationWorkflow>()
                .ExecuteNextAsync(context.WorkDirectory, new DriveInfo(context.WorkDirectory).AvailableFreeSpace,
                    TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), token)), "Preparation did not claim the smoke video.");
            var video = await context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().Videos
                .AsNoTracking().SingleAsync(row => row.VideoId == context.VideoId, token));
            Assert.True(video.Status == "ready", "The real smoke video did not finish preparation.");
            Assert.True(video.DurationSeconds is > 0 and <= 60, "Use a spoken lesson of at most 60 seconds.");
            var attempt = await WaitForReadyAsync(context, token);
            Assert.True(attempt.IsCurrent && attempt.ReadyAt is not null && attempt.WhisperJobId is not null,
                "The real result was not persisted as the current ready revision.");
            var content = await context.WithScopeAsync(provider => provider.GetRequiredService<IGetVideoTranscription>()
                .ExecuteAsync(new(context.VideoId), token));
            Assert.True(content.DurationMs > 0 && !string.IsNullOrWhiteSpace(content.Language)
                && content.Segments.Count > 0 && content.Segments.All(segment =>
                    !string.IsNullOrWhiteSpace(segment.Text) && segment.Text.Any(char.IsLetter)),
                "The teacher must be able to read nonempty speech segments from persisted content.");
            // Verify worker cleanup before fixture cleanup can conceal a leaked object.
            Assert.True(attempt.AudioDeletedAt is not null, "The worker did not record audio deletion.");
            Assert.True(await context.IsAudioPrefixEmptyAsync(token), "The smoke transcription prefix still contains audio.");
            var audioRoot = Path.Combine(context.WorkDirectory, "transcription");
            Assert.True(!Directory.Exists(audioRoot) || !Directory.EnumerateFileSystemEntries(audioRoot).Any(),
                "The worker left temporary extraction files.");
            Assert.Empty(Directory.EnumerateFiles(context.WorkDirectory, "*.bin", SearchOption.AllDirectories));
        }
        catch (XunitException) { throw; }
        catch (Exception)
        {
            throw new XunitException("The real-service smoke failed or timed out. Check infrastructure and account configuration; sensitive exception details are suppressed.");
        }
    }

    private static async Task<Transcription> WaitForReadyAsync(TranscriptionRealServiceSmokeContext context, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            await context.WithScopeAsync(provider => provider.GetRequiredService<ITranscriptionWorkflow>()
                .ExecuteNextAsync(new(context.WorkDirectory, new DriveInfo(context.WorkDirectory).AvailableFreeSpace), token));
            var attempt = await context.WithScopeAsync(provider => provider.GetRequiredService<MediaDbContext>().Transcriptions
                .AsNoTracking().SingleAsync(row => row.VideoId == context.VideoId, token));
            Assert.True(attempt.Status != "failed", "The real-service transcription reached a failed state.");
            if (attempt.Status == "ready") return attempt;
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }
}
