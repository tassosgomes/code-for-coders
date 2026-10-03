using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.S3;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoPreparationTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoPreparation_1080pPublishesEncryptedHlsAndReadyOutbox))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_1080pPublishesEncryptedHlsAndReadyOutbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoAsync(1920, 1080, 20, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var prepared = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("ready", prepared.Status);
        Assert.Equal(20, prepared.DurationSeconds);
        Assert.True(prepared.StoredBytes > 0);
        Assert.Equal(VideoPreparationTestContext.MasterKeyId, prepared.MasterKeyId);
        Assert.NotNull(prepared.EncryptedVideoKey);

        var allObjects = await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken);
        Assert.Contains(allObjects, key => key.EndsWith("/hls/master.m3u8", StringComparison.Ordinal));
        Assert.Contains(allObjects, key => key.EndsWith("/hls/1080p.m3u8", StringComparison.Ordinal));
        Assert.Contains(allObjects, key => key.EndsWith("/hls/720p.m3u8", StringComparison.Ordinal));
        Assert.Contains(allObjects, key => key.EndsWith("/hls/480p.m3u8", StringComparison.Ordinal));
        Assert.DoesNotContain(allObjects, key => key.EndsWith("/original", StringComparison.Ordinal));
        Assert.DoesNotContain(allObjects, key => key.Contains("key.bin", StringComparison.Ordinal));

        var messages = await ReadReadyMessagesAsync(context, video.VideoId, cancellationToken);
        var readyMessage = Assert.Single(messages);
        using var payload = JsonDocument.Parse(readyMessage.Payload);
        var root = payload.RootElement;
        Assert.Equal(readyMessage.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(video.TenantId, root.GetProperty("tenantId").GetGuid());
        Assert.Equal(video.VideoId, root.GetProperty("videoId").GetGuid());
        Assert.Equal(20, root.GetProperty("durationSeconds").GetInt32());
        Assert.False(root.TryGetProperty("title", out _));
        var replay = Assert.Single(await ReadReadyMessagesAsync(context, video.VideoId, cancellationToken));
        Assert.Equal(readyMessage.Id, replay.Id);
        Assert.Equal(readyMessage.Payload, replay.Payload);

        var deliveries = await context.PublishRepeatedlyAsync(readyMessage, 2, cancellationToken);
        Assert.Equal(2, deliveries.Count);
        foreach (var delivery in deliveries)
        {
            Assert.Equal("midia.ativo-pronto.v1", delivery.RoutingKey);
            Assert.Equal(readyMessage.Id.ToString(), delivery.MessageId);
            using var deliveredPayload = JsonDocument.Parse(delivery.Body);
            MediaMessages.AssertSends(delivery.RoutingKey, deliveredPayload.RootElement);
            Assert.Equal(readyMessage.Id, deliveredPayload.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(20, deliveredPayload.RootElement.GetProperty("durationSeconds").GetInt32());
            Assert.False(deliveredPayload.RootElement.TryGetProperty("title", out _));
        }

        using var anonymous = context.CreateAnonymousS3Client();
        var publicAssetKeys = allObjects.Where(key =>
            key.EndsWith(".m3u8", StringComparison.Ordinal)
            || key.EndsWith(".ts", StringComparison.Ordinal));
        Assert.NotEmpty(publicAssetKeys);
        foreach (var objectKey in publicAssetKeys)
        {
            var denied = await Assert.ThrowsAsync<AmazonS3Exception>(() => anonymous.GetObjectAsync(
                MediaIntegrationFixture.MinioBucketName,
                objectKey,
                cancellationToken));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var downloadedHls = Path.Combine(context.RootDirectory, "downloaded-hls");
        await context.DownloadHlsTreeAsync(video, downloadedHls, cancellationToken);
        var playlistPath = Path.Combine(downloadedHls, "1080p.m3u8");
        var playlist = await File.ReadAllTextAsync(playlistPath, cancellationToken);
        Assert.Contains($"URI=\"c4c-key:{video.VideoId:D}\"", playlist, StringComparison.Ordinal);
        var unavailableKeyResult = await context.RunFfmpegAsync(
            ["-hide_banner", "-loglevel", "error", "-protocol_whitelist", "file,crypto,data", "-allowed_extensions", "ALL", "-i", playlistPath, "-f", "null", "-"],
            cancellationToken);
        Assert.NotEqual(0, unavailableKeyResult.ExitCode);

        var videoKey = context.UnprotectKey(video.VideoId, prepared.MasterKeyId!, prepared.EncryptedVideoKey!);
        try
        {
            var keyRepresentations = new[]
            {
                Convert.ToBase64String(videoKey),
                Convert.ToHexString(videoKey),
                Convert.ToHexStringLower(videoKey),
            };
            var observedText = context.Logs.Entries
                .Append(readyMessage.Payload)
                .Concat(deliveries.Select(delivery => delivery.Body))
                .ToArray();
            Assert.NotEmpty(context.Logs.Entries);
            foreach (var representation in keyRepresentations)
            {
                Assert.DoesNotContain(observedText, text => text.Contains(representation, StringComparison.OrdinalIgnoreCase));
            }

            var keyPath = Path.Combine(context.RootDirectory, "playback-key.bin");
            await File.WriteAllBytesAsync(keyPath, videoKey, cancellationToken);
            var keyUri = new Uri(keyPath).AbsoluteUri;
            foreach (var playlistFile in Directory.EnumerateFiles(downloadedHls, "*.m3u8", SearchOption.AllDirectories))
            {
                var contents = await File.ReadAllTextAsync(playlistFile, cancellationToken);
                await File.WriteAllTextAsync(
                    playlistFile,
                    contents.Replace($"c4c-key:{video.VideoId:D}", keyUri, StringComparison.Ordinal),
                    cancellationToken);
            }

            var playable = await context.RunFfmpegAsync(
                ["-hide_banner", "-loglevel", "error", "-protocol_whitelist", "file,crypto,data", "-allowed_extensions", "ALL", "-i", playlistPath, "-f", "null", "-"],
                cancellationToken);
            Assert.True(playable.ExitCode == 0, playable.StandardError);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(videoKey);
        }
    }

    [Fact(DisplayName = nameof(VideoPreparation_720pCreatesOnly720pAnd480p))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_720pCreatesOnly720pAnd480p()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateQueuedVideoAsync(1280, 720, 20, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var keys = await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}/hls", cancellationToken);
        Assert.Contains(keys, key => key.EndsWith("/hls/720p.m3u8", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.EndsWith("/hls/480p.m3u8", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, key => key.EndsWith("/hls/1080p.m3u8", StringComparison.Ordinal));
    }

    [Fact(DisplayName = nameof(VideoPreparation_DoesNotClaimWhenDiskIsBelowThreeTimesSourceSize))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_DoesNotClaimWhenDiskIsBelowThreeTimesSourceSize()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);

        Assert.False(await context.ExecuteNextAsync((video.FileSize * 3) - 1, cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("received", stored.Status);
        Assert.Equal(0, stored.PreparationAttempts);
    }

    [Fact(DisplayName = nameof(VideoPreparation_ClaimsSourceMetadataStoredOnVideo))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_ClaimsSourceMetadataStoredOnVideo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateLegacyQueuedVideoAsync(640, 360, 3, cancellationToken);

        Assert.True(await context.ExecuteNextAsync(50L * 1024 * 1024 * 1024, cancellationToken));

        var prepared = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("ready", prepared.Status);
        Assert.Equal(video.OriginalObjectKey, prepared.OriginalObjectKey);
        Assert.Equal(video.FileSize, prepared.OriginalSizeBytes);
        Assert.Equal(3, prepared.DurationSeconds);
    }

    [Fact(DisplayName = nameof(VideoPreparation_OnlyOneWorkerClaimsTheSameVideo))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_OnlyOneWorkerClaimsTheSameVideo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await using var firstScope = context.CreateScope();
        await using var secondScope = context.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IVideoPreparationRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IVideoPreparationRepository>();

        var claims = await Task.WhenAll(
            firstRepository.ClaimNextAsync(now, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken),
            secondRepository.ClaimNextAsync(now, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));

        Assert.Single(claims, claim => claim is not null);
        Assert.Contains(claims, claim => claim?.VideoId == video.VideoId);
    }

    [Fact(DisplayName = nameof(VideoPreparation_ExpiredLeaseUsesTheFirstRetryBackoff))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_ExpiredLeaseUsesTheFirstRetryBackoff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        var previousClaim = DateTimeOffset.UtcNow.AddMinutes(-10);
        var firstLease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(previousClaim, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));
        Assert.NotNull(firstLease);

        var recovered = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationWorkflow>()
            .RecoverExpiredLeasesAsync(100, cancellationToken));
        var deferredClaim = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));
        await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
        var nextLease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));

        Assert.Equal(1, recovered);
        Assert.Null(deferredClaim);
        Assert.NotNull(nextLease);
        Assert.Equal(video.VideoId, nextLease.VideoId);
        Assert.NotEqual(firstLease.LeaseId, nextLease.LeaseId);
    }

    [Fact(DisplayName = nameof(VideoPreparation_RenewsOnlyTheActiveLease))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_RenewsOnlyTheActiveLease()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var lease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(now, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));
        Assert.NotNull(lease);
        var renewedUntil = now.AddMinutes(10);

        var renewed = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .RenewLeaseAsync(video.VideoId, lease.LeaseId, renewedUntil, cancellationToken));
        var wrongLease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .RenewLeaseAsync(video.VideoId, Guid.CreateVersion7(), renewedUntil.AddMinutes(1), cancellationToken));
        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);

        Assert.True(renewed);
        Assert.False(wrongLease);
        Assert.InRange(
            (stored.PreparationLeaseUntil!.Value - renewedUntil).Duration(),
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1));
    }

    [Fact(DisplayName = nameof(VideoPreparation_CleanupDeletesOriginalOutsideTheReadyCommit))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_CleanupDeletesOriginalOutsideTheReadyCommit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        await context.UploadOriginalBytesAsync(video.OriginalObjectKey, [1, 2, 3], cancellationToken);
        await context.SeedReadyVideoAsync(video, cancellationToken);

        var deleted = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationWorkflow>()
            .CleanupFinalArtifactsAsync(100, cancellationToken));
        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        var keys = await context.ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}", cancellationToken);

        Assert.Equal(1, deleted);
        Assert.NotNull(stored.OriginalDeletedAt);
        Assert.DoesNotContain(keys, key => key.EndsWith("/original", StringComparison.Ordinal));
    }

    [Fact(DisplayName = nameof(VideoPreparation_KeyProtectorAuthenticatesTheVideoId))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_KeyProtectorAuthenticatesTheVideoId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var videoId = Guid.CreateVersion7();
        var key = RandomNumberGenerator.GetBytes(16);
        var protectedKey = await context.WithScopeAsync(scope => Task.FromResult(
            scope.GetRequiredService<IVideoKeyProtector>().Protect(videoId, key)));

        var unprotected = context.UnprotectKey(videoId, protectedKey.MasterKeyId, protectedKey.Ciphertext);
        try
        {
            Assert.Equal(key, unprotected);
            Assert.ThrowsAny<CryptographicException>(() => context.UnprotectKey(
                Guid.CreateVersion7(),
                protectedKey.MasterKeyId,
                protectedKey.Ciphertext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(unprotected);
        }
    }

    [Fact(DisplayName = nameof(VideoPreparation_InvalidMasterKeyIsRejected))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_InvalidMasterKeyIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var valid = await context.WithScopeAsync(scope => Task.FromResult(scope.GetRequiredService<IOptions<VideoPreparationOptions>>().Value));
        Assert.True(valid.HasValidWorkerSettings());

        var invalid = new VideoPreparationOptions
        {
            WorkDirectory = "work",
            MasterKey = "not-base64",
            MasterKeyId = "test-key",
        };

        Assert.False(invalid.HasValidWorkerSettings());
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddOptions<VideoPreparationOptions>()
            .Configure(options =>
            {
                options.WorkDirectory = invalid.WorkDirectory;
                options.MasterKey = invalid.MasterKey;
                options.MasterKeyId = invalid.MasterKeyId;
            })
            .Validate(options => options.HasValidWorkerSettings(), "Media video preparation configuration is invalid.")
            .ValidateOnStart();
        using var host = hostBuilder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(cancellationToken));
    }

    [Fact(DisplayName = nameof(VideoPreparation_WorkerContinuesAfterUnexpectedItemAndScanFailures))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_WorkerContinuesAfterUnexpectedItemAndScanFailures()
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), $"media-worker-{Guid.CreateVersion7():N}");
        var workflow = new ThrowOncePreparationWorkflow();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVideoPreparationWorkflow>(workflow);
        await using var provider = services.BuildServiceProvider();
        var settings = new VideoPreparationOptions
        {
            WorkDirectory = workDirectory,
            PollingIntervalSeconds = 1,
            MaxConcurrency = 1,
        };
        var worker = new VideoPreparationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            TimeProvider.System,
            provider.GetRequiredService<ILogger<VideoPreparationWorker>>());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        await worker.StartAsync(cancellation.Token);
        await workflow.SecondCycleStarted.Task.WaitAsync(cancellation.Token);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(workflow.RecoveryCalls >= 2);
        Assert.True(workflow.ProcessCalls >= 2);
        if (Directory.Exists(workDirectory))
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    [Theory(DisplayName = nameof(VideoPreparation_QualityLadderNeverUpscalesAndRespectsBitrateCeilings))]
    [Trait("Layer", "Media video preparation - Integration")]
    [InlineData(1920, 1080, new[] { "1080p", "720p", "480p" })]
    [InlineData(3840, 2160, new[] { "1080p", "720p", "480p" })]
    [InlineData(1280, 720, new[] { "720p", "480p" })]
    [InlineData(960, 540, new[] { "480p" })]
    [InlineData(1024, 576, new[] { "480p" })]
    [InlineData(854, 480, new[] { "480p" })]
    [InlineData(640, 360, new[] { "360p" })]
    public void VideoPreparation_QualityLadderNeverUpscalesAndRespectsBitrateCeilings(
        int width,
        int height,
        string[] expectedQualities)
    {
        var qualities = VideoQualityLadder.Select(width, height);

        Assert.Equal(expectedQualities, qualities.Select(quality => quality.Name));
        Assert.All(qualities, quality =>
        {
            Assert.True(quality.Height <= height);
            Assert.Equal(0, quality.Width % 2);
            var ceiling = quality.Height switch
            {
                1080 => 5_000_000L,
                720 => 3_000_000L,
                _ => 1_200_000L,
            };
            Assert.InRange(quality.Bandwidth, 1, ceiling);
        });
    }

    [Fact(DisplayName = nameof(VideoPreparation_ReadyCommitIsRejectedAfterTheLeaseIsLost))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_ReadyCommitIsRejectedAfterTheLeaseIsLost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = await VideoPreparationTestContext.CreateAsync(factory, cancellationToken);
        var video = await context.CreateDatabaseOnlyVideoAsync(1_024, cancellationToken);
        var staleLease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(DateTimeOffset.UtcNow.AddMinutes(-10), TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));
        Assert.NotNull(staleLease);

        await using var staleScope = context.CreateScope();
        var staleVideo = await staleScope.ServiceProvider.GetRequiredService<IVideoPreparationRepository>()
            .GetClaimedAsync(video.VideoId, staleLease.LeaseId, cancellationToken);
        Assert.NotNull(staleVideo);

        await context.ExpirePreparationLeaseAsync(video.VideoId, cancellationToken);
        await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationWorkflow>()
            .RecoverExpiredLeasesAsync(100, cancellationToken));
        await context.MakePreparationEligibleAsync(video.VideoId, cancellationToken);
        var activeLease = await context.WithScopeAsync(scope => scope.GetRequiredService<IVideoPreparationRepository>()
            .ClaimNextAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), 1_000_000, cancellationToken));
        Assert.NotNull(activeLease);

        staleVideo.MarkReady(staleLease.LeaseId, 20, 1_024, new byte[45], VideoPreparationTestContext.MasterKeyId);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => staleScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>()
            .CommitAsync(cancellationToken));

        var stored = await ReadVideoAsync(context, video.VideoId, cancellationToken);
        Assert.Equal("preparing", stored.Status);
        Assert.Equal(activeLease.LeaseId, stored.PreparationLeaseId);
        Assert.Null(stored.EncryptedVideoKey);
        Assert.Empty(await ReadReadyMessagesAsync(context, video.VideoId, cancellationToken));
    }

    [Fact(DisplayName = nameof(VideoPreparation_ExpiredUploadWorkerContinuesAfterUnexpectedFailure))]
    [Trait("Layer", "Media video preparation - Integration")]
    public async Task VideoPreparation_ExpiredUploadWorkerContinuesAfterUnexpectedFailure()
    {
        var expireUploads = new ThrowOnceExpirePendingVideoUploads();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IExpirePendingVideoUploads>(expireUploads);
        await using var provider = services.BuildServiceProvider();
        var worker = new ExpiredVideoUploadWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FastTimerTimeProvider(),
            provider.GetRequiredService<ILogger<ExpiredVideoUploadWorker>>());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        await worker.StartAsync(cancellation.Token);
        await expireUploads.SecondScanStarted.Task.WaitAsync(cancellation.Token);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(expireUploads.Calls >= 2);
    }

    private static Task<Video> ReadVideoAsync(
        VideoPreparationTestContext context,
        Guid videoId,
        CancellationToken cancellationToken)
        => context.WithScopeAsync(scope => scope.GetRequiredService<MediaDbContext>().Videos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(video => video.VideoId == videoId, cancellationToken));

    private static Task<OutboxMessage[]> ReadReadyMessagesAsync(
        VideoPreparationTestContext context,
        Guid videoId,
        CancellationToken cancellationToken)
        => context.WithScopeAsync(async scope =>
        {
            var messages = await scope.GetRequiredService<MediaDbContext>().OutboxMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(message => message.RoutingKey == "midia.ativo-pronto.v1")
                .ToArrayAsync(cancellationToken);
            return messages.Where(message =>
            {
                using var document = JsonDocument.Parse(message.Payload);
                return document.RootElement.GetProperty("videoId").GetGuid() == videoId;
            }).ToArray();
        });

    private sealed class ThrowOncePreparationWorkflow : IVideoPreparationWorkflow
    {
        private int recoveryCalls;
        private int processCalls;

        public TaskCompletionSource SecondCycleStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RecoveryCalls => Volatile.Read(ref recoveryCalls);

        public int ProcessCalls => Volatile.Read(ref processCalls);

        public Task<bool> ExecuteNextAsync(
            string workDirectory,
            long availableDiskBytes,
            TimeSpan leaseDuration,
            TimeSpan leaseRenewalInterval,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref processCalls);
            if (call == 1)
            {
                throw new InvalidOperationException("Expected test failure.");
            }

            if (call == 2)
            {
                SecondCycleStarted.TrySetResult();
                return Task.FromResult(false);
            }

            return Task.FromResult(false);
        }

        public Task<int> RecoverExpiredLeasesAsync(int batchSize, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref recoveryCalls) == 1)
            {
                throw new InvalidOperationException("Expected test scan failure.");
            }

            return Task.FromResult(0);
        }

        public Task<int> CleanupFinalArtifactsAsync(int batchSize, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class ThrowOnceExpirePendingVideoUploads : IExpirePendingVideoUploads
    {
        private int calls;

        public TaskCompletionSource SecondScanStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls => Volatile.Read(ref calls);

        public Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("Expected test scan failure.");
            }

            SecondScanStarted.TrySetResult();
            return Task.FromResult(0);
        }

        public Task<bool> ExpireAsync(Guid uploadId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    private sealed class FastTimerTimeProvider : TimeProvider
    {
        private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => System.CreateTimer(
                callback,
                state,
                dueTime == Timeout.InfiniteTimeSpan ? dueTime : Tick,
                period == Timeout.InfiniteTimeSpan ? period : Tick);
    }
}
