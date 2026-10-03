using System.Text.Json;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoPreparationFailureTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoPreparationFailure_UnreadableFileFailsImmediatelyAndPublishesAnEvent))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_UnreadableFileFailsImmediatelyAndPublishesAnEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoWithBytesAsync([0x00, 0x7f, 0x00, 0x01, 0xff], cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("failed", stored.Status);
        Assert.Equal(VideoFailureReasons.UnreadableFile, stored.FailureReason);
        Assert.Equal(1, stored.PreparationAttempts);
        Assert.Empty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken));
        Assert.False(Directory.Exists(Path.Combine(context.WorkDirectory, video.VideoId.ToString("N"))));
        await AssertFailureEventPublishedAsync(context, video.VideoId, VideoFailureReasons.UnreadableFile, cancellationToken);
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_UnsupportedDecoderFailsImmediately))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_UnsupportedDecoderFailsImmediately()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoWithUnknownCodecAsync(cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("failed", stored.Status);
        Assert.Equal(VideoFailureReasons.UnsupportedFormat, stored.FailureReason);
        Assert.Equal(1, stored.PreparationAttempts);
        Assert.Empty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken));
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_VideoOverThreeHoursFailsImmediately))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_VideoOverThreeHoursFailsImmediately()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateLongQueuedVideoAsync(10_860, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("failed", stored.Status);
        Assert.Equal(VideoFailureReasons.DurationExceeded, stored.FailureReason);
        Assert.Equal(1, stored.PreparationAttempts);
        Assert.Empty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken));
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_StorageRecoveryRetriesAndPreparesTheVideo))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_StorageRecoveryRetriesAndPreparesTheVideo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 1));
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var retry = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("received", retry.Status);
        Assert.Equal(1, retry.PreparationAttempts);
        Assert.InRange(
            (retry.NextPreparationAt!.Value - DateTimeOffset.UtcNow.AddMinutes(1)).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5));
        Assert.Contains(
            await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken),
            key => key.EndsWith("/original", StringComparison.Ordinal));

        await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var ready = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("ready", ready.Status);
        Assert.Equal(2, ready.PreparationAttempts);
        Assert.Equal(2, context.DownloadAttemptCount);
        Assert.NotEmpty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}/hls", cancellationToken));
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_TransientFailuresUseBackoffAndFailOnTheThirdAttempt))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_TransientFailuresUseBackoffAndFailOnTheThirdAttempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(StorageDownloadFailures: 3));
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);

        for (var attempt = 1; attempt <= Video.MaximumPreparationAttempts; attempt++)
        {
            await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
            Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));
            var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
            Assert.Equal(attempt, stored.PreparationAttempts);
            if (attempt < Video.MaximumPreparationAttempts)
            {
                Assert.Equal("received", stored.Status);
                var expectedDelay = attempt == 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
                Assert.InRange(
                    (stored.NextPreparationAt!.Value - DateTimeOffset.UtcNow.Add(expectedDelay)).Duration(),
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(5));
            }
            else
            {
                Assert.Equal("failed", stored.Status);
                Assert.Equal(VideoFailureReasons.PreparationFailed, stored.FailureReason);
            }
        }

        Assert.Equal(Video.MaximumPreparationAttempts, context.DownloadAttemptCount);
        Assert.Empty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken));
        await AssertFailureEventPublishedAsync(context, video.VideoId, VideoFailureReasons.PreparationFailed, cancellationToken);
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_ExpiredLeasesUseBothBackoffsAndFailOnTheThirdAttempt))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_ExpiredLeasesUseBothBackoffsAndFailOnTheThirdAttempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);

        for (var attempt = 1; attempt <= Video.MaximumPreparationAttempts; attempt++)
        {
            await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
            var lease = await ClaimAsync(context, cancellationToken);
            Assert.NotNull(lease);
            await context.ExpirePreparationLeaseAsync(video.VideoId, cancellationToken);
            await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationWorkflow>()
                .RecoverExpiredLeasesAsync(100, cancellationToken));
            var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
            Assert.Equal(attempt, stored.PreparationAttempts);
            if (attempt < Video.MaximumPreparationAttempts)
            {
                Assert.Equal("received", stored.Status);
                var expectedDelay = attempt == 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
                Assert.InRange(
                    (stored.NextPreparationAt!.Value - DateTimeOffset.UtcNow.Add(expectedDelay)).Duration(),
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(5));
            }
            else
            {
                Assert.Equal("failed", stored.Status);
                Assert.Equal(VideoFailureReasons.PreparationFailed, stored.FailureReason);
            }
        }

        Assert.Empty(await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken));
        await AssertFailureOutboxMatches(context, video.VideoId, VideoFailureReasons.PreparationFailed, cancellationToken);
    }

    [Fact(DisplayName = nameof(VideoPreparationFailure_ConfiguredThreadsReachTheFfmpegCommand))]
    [Trait("Layer", "Media video preparation failure - Integration")]
    public async Task VideoPreparationFailure_ConfiguredThreadsReachTheFfmpegCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(
            factory,
            cancellationToken,
            new VideoPreparationTestContext.TestOverrides(Threads: 4, CaptureFfmpegArguments: true));
        var video = await context.CreateQueuedVideoAsync(320, 240, 1, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var arguments = await File.ReadAllTextAsync(context.FfmpegArgumentCapturePath, cancellationToken);
        Assert.Contains("-threads:v\n4", arguments, StringComparison.Ordinal);
        Assert.Equal("ready", (await ReadVideoAsync(context, video.VideoId, cancellationToken)).Status);
    }

    private static async Task<StoredVideo> ReadVideoAsync(
        VideoPreparationTestContext context,
        Guid videoId,
        CancellationToken cancellationToken)
        => await context.WithScopeAsync(async scope => await scope.GetRequiredService<MediaDbContext>()
            .Videos.IgnoreQueryFilters().AsNoTracking()
            .Where(video => video.VideoId == videoId)
            .Select(video => new StoredVideo(
                video.Status,
                video.FailureReason,
                video.PreparationAttempts,
                video.NextPreparationAt))
            .SingleAsync(cancellationToken));

    private static Task<VideoPreparationLease?> ClaimAsync(
        VideoPreparationTestContext context,
        CancellationToken cancellationToken)
        => context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5),
                50L * 1024 * 1024 * 1024,
                cancellationToken));

    private static async Task AssertFailureEventPublishedAsync(
        VideoPreparationTestContext context,
        Guid videoId,
        string reason,
        CancellationToken cancellationToken)
    {
        var message = await AssertFailureOutboxMatches(context, videoId, reason, cancellationToken);
        var delivered = await context.PublishRepeatedlyAsync(message, 1, cancellationToken);
        var published = Assert.Single(delivered);
        Assert.Equal("midia.preparacao-falhou.v1", published.RoutingKey);
        Assert.False(string.IsNullOrWhiteSpace(published.CorrelationId));
        Assert.Equal(published.CorrelationId, published.CorrelationHeader);
        using var payload = JsonDocument.Parse(published.Body);
        MediaMessages.AssertSends(published.RoutingKey, payload.RootElement);
        Assert.Equal(
            new[] { "eventId", "tenantId", "videoId", "occurredAt", "reason" }.Order(),
            payload.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.NotEqual(Guid.Empty, payload.RootElement.GetProperty("eventId").GetGuid());
        Assert.NotEqual(Guid.Empty, payload.RootElement.GetProperty("tenantId").GetGuid());
        Assert.True(payload.RootElement.GetProperty("occurredAt").GetDateTimeOffset() > DateTimeOffset.MinValue);
        Assert.Equal(reason, payload.RootElement.GetProperty("reason").GetString());
        Assert.Equal(videoId, payload.RootElement.GetProperty("videoId").GetGuid());
    }

    private static async Task<OutboxMessage> AssertFailureOutboxMatches(
        VideoPreparationTestContext context,
        Guid videoId,
        string reason,
        CancellationToken cancellationToken)
    {
        var messages = await context.WithScopeAsync(async scope => await scope.GetRequiredService<MediaDbContext>()
            .OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .Where(message => message.RoutingKey == "midia.preparacao-falhou.v1")
            .ToArrayAsync(cancellationToken));
        var matching = messages.Where(message =>
        {
            using var payload = JsonDocument.Parse(message.Payload);
            return payload.RootElement.GetProperty("videoId").GetGuid() == videoId;
        }).ToArray();
        var message = Assert.Single(matching);
        using var eventPayload = JsonDocument.Parse(message.Payload);
        Assert.Equal(reason, eventPayload.RootElement.GetProperty("reason").GetString());
        return message;
    }

    private sealed record StoredVideo(
        string Status,
        string? FailureReason,
        int PreparationAttempts,
        DateTimeOffset? NextPreparationAt);
}
