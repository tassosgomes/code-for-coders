using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Data.Videos;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using CodeForCoders.Media.Application.UseCases.Videos.PrepareVideo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

internal sealed class VideoPreparationTestContext : IAsyncDisposable
{
    public const string MasterKeyId = "integration-master-v1";

    private readonly VideoLibraryApiFactory factory;
    private readonly ServiceProvider services;
    private readonly List<TestVideo> videos = [];
    private readonly StorageFailureController storageFailures;

    private VideoPreparationTestContext(
        VideoLibraryApiFactory factory,
        ServiceProvider services,
        CapturedLogProvider logs,
        StorageFailureController storageFailures,
        string ffmpegPath,
        string ffprobePath,
        string rootDirectory)
    {
        this.factory = factory;
        this.services = services;
        Logs = logs;
        this.storageFailures = storageFailures;
        FfmpegPath = ffmpegPath;
        FfprobePath = ffprobePath;
        RootDirectory = rootDirectory;
        WorkDirectory = Path.Combine(rootDirectory, "work");
    }

    public string FfmpegPath { get; }

    public string FfprobePath { get; }

    public string RootDirectory { get; }

    public string WorkDirectory { get; }

    public string FfmpegArgumentCapturePath => Path.Combine(RootDirectory, "ffmpeg-arguments.log");

    public CapturedLogProvider Logs { get; }

    public int DownloadAttemptCount => storageFailures.DownloadAttemptCount;

    public AsyncServiceScope CreateScope()
        => services.CreateAsyncScope();

    public static async Task<VideoPreparationTestContext> CreateAsync(
        VideoLibraryApiFactory factory,
        CancellationToken cancellationToken,
        TestOverrides? overrides = null)
    {
        overrides ??= new TestOverrides();
        var (ffmpegPath, ffprobePath) = await FfmpegTools.EnsureAvailableAsync(cancellationToken);
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"media-preparation-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(rootDirectory);
        Directory.CreateDirectory(Path.Combine(rootDirectory, "work"));
        var transcoderFfmpegPath = overrides.TranscoderFfmpegPath ?? ffmpegPath;
        if (overrides.CaptureFfmpegArguments)
        {
            transcoderFfmpegPath = await CreateFfmpegWrapperAsync(
                rootDirectory,
                ffmpegPath,
                overrides.CaptureFfmpegArguments,
                cancellationToken);
        }

        var logs = new CapturedLogProvider();
        var storageFailures = new StorageFailureController(overrides.StorageDownloadFailures);
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<MediaDbContext>(options => options.UseNpgsql(
            factory.VideoPreparationConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MediaSchema.Name)));
        services.AddScoped<IVideoPreparationRepository, VideoPreparationRepository>();
        services.AddScoped<IUnitOfWork, MediaUnitOfWork>();
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<S3MediaStorageAdapter>();
        services.AddScoped<IMediaStoragePort>(provider => new FaultInjectingMediaStoragePort(
            provider.GetRequiredService<S3MediaStorageAdapter>(),
            storageFailures));
        services.AddSingleton<S3MediaClientPair>();
        services.AddSingleton<IVideoKeyProtector, AesVideoKeyProtector>();
        services.AddSingleton<IVideoTranscoder>(provider =>
            new FfmpegVideoTranscoder(provider.GetRequiredService<IOptions<VideoPreparationOptions>>()));
        services.AddScoped<IVideoPreparationWorkflow, PrepareVideo>();
        services.Configure<AwsMediaOptions>(options =>
        {
            options.Region = "us-east-1";
            options.BucketName = MediaIntegrationFixture.MinioBucketName;
            options.ObjectKeyPrefix = "media";
            options.EndpointInternal = factory.MinioEndpoint;
            options.EndpointPublic = factory.MinioEndpoint;
            options.AccessKeyId = MediaIntegrationFixture.MinioAccessKey;
            options.SecretAccessKey = MediaIntegrationFixture.MinioSecretKey;
            options.ForcePathStyle = true;
        });
        services.Configure<VideoPreparationOptions>(options =>
        {
            options.WorkDirectory = Path.Combine(rootDirectory, "work");
            options.FfmpegPath = transcoderFfmpegPath;
            options.FfprobePath = ffprobePath;
            options.Threads = overrides.Threads;
            options.MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            options.MasterKeyId = MasterKeyId;
        });
        services.Configure<RabbitMqOptions>(options =>
        {
            options.Host = factory.RabbitMqEndpoint.Host;
            options.Port = factory.RabbitMqEndpoint.Port;
            options.Username = "code_for_coders";
            options.Password = "code_for_coders";
        });
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<RabbitMqTopologyInitializer>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var context = new VideoPreparationTestContext(
            factory,
            provider,
            logs,
            storageFailures,
            ffmpegPath,
            ffprobePath,
            rootDirectory);
        await context.ResetDatabaseAsync(cancellationToken);
        return context;
    }

    public async Task<TestVideo> CreateQueuedVideoAsync(
        int width,
        int height,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        var videoId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var originalObjectKey = $"{tenantId:D}/{videoId:D}/original";
        var sourcePath = Path.Combine(RootDirectory, $"source-{videoId:N}.mp4");
        await CreateSourceVideoAsync(sourcePath, width, height, durationSeconds, cancellationToken);
        var fileSize = new FileInfo(sourcePath).Length;
        await UploadOriginalAsync(originalObjectKey, sourcePath, cancellationToken);
        var video = CreateDomainVideo(videoId, tenantId, originalObjectKey, fileSize, DateTimeOffset.UtcNow);
        await AddVideoAsync(video, cancellationToken);
        var testVideo = new TestVideo(videoId, tenantId, originalObjectKey, sourcePath, fileSize);
        videos.Add(testVideo);
        return testVideo;
    }

    public async Task<TestVideo> CreateQueuedVideoWithBytesAsync(
        byte[] contents,
        CancellationToken cancellationToken)
    {
        var videoId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var originalObjectKey = $"{tenantId:D}/{videoId:D}/original";
        var sourcePath = Path.Combine(RootDirectory, $"source-{videoId:N}.mp4");
        await File.WriteAllBytesAsync(sourcePath, contents, cancellationToken);
        await UploadOriginalBytesAsync(originalObjectKey, contents, cancellationToken);
        var video = CreateDomainVideo(videoId, tenantId, originalObjectKey, contents.Length, DateTimeOffset.UtcNow);
        await AddVideoAsync(video, cancellationToken);
        var testVideo = new TestVideo(videoId, tenantId, originalObjectKey, sourcePath, contents.Length);
        videos.Add(testVideo);
        return testVideo;
    }

    public async Task<TestVideo> CreateQueuedVideoWithUnknownCodecAsync(CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(RootDirectory, "unknown-codec.avi");
        await RunProcessAsync(
            FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-y",
                "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=1",
                "-c:v", "mpeg4", "-vtag", "XVID", sourcePath,
            ],
            cancellationToken);

        var contents = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        var codecTag = "XVID"u8;
        var unknownTag = "ZZZZ"u8;
        var replacements = 0;
        for (var index = 0; index <= contents.Length - codecTag.Length; index++)
        {
            if (!contents.AsSpan(index, codecTag.Length).SequenceEqual(codecTag))
            {
                continue;
            }

            unknownTag.CopyTo(contents.AsSpan(index, unknownTag.Length));
            replacements++;
        }

        Assert.True(replacements > 0, "The generated AVI did not contain its expected video codec tag.");
        return await CreateQueuedVideoWithBytesAsync(contents, cancellationToken);
    }

    public async Task<TestVideo> CreateLongQueuedVideoAsync(
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        var videoId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var originalObjectKey = $"{tenantId:D}/{videoId:D}/original";
        var shortSourcePath = Path.Combine(RootDirectory, $"short-source-{videoId:N}.mp4");
        var sourcePath = Path.Combine(RootDirectory, $"source-{videoId:N}.mp4");
        await CreateSourceVideoAsync(shortSourcePath, 256, 144, 1, cancellationToken);
        await RunProcessAsync(
            FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-y", "-stream_loop", "-1",
                "-i", shortSourcePath, "-t", durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-c", "copy", sourcePath,
            ],
            cancellationToken);
        var fileSize = new FileInfo(sourcePath).Length;
        await UploadOriginalAsync(originalObjectKey, sourcePath, cancellationToken);
        var video = CreateDomainVideo(videoId, tenantId, originalObjectKey, fileSize, DateTimeOffset.UtcNow);
        await AddVideoAsync(video, cancellationToken);
        var testVideo = new TestVideo(videoId, tenantId, originalObjectKey, sourcePath, fileSize);
        videos.Add(testVideo);
        return testVideo;
    }

    public async Task<TestVideo> CreateLegacyQueuedVideoAsync(
        int width,
        int height,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        var uploadedAt = DateTimeOffset.UtcNow;
        var tenantId = Guid.CreateVersion7();
        var uploaderAccountId = Guid.CreateVersion7();
        var sourcePath = Path.Combine(RootDirectory, $"legacy-source-{Guid.CreateVersion7():N}.mp4");
        await CreateSourceVideoAsync(sourcePath, width, height, durationSeconds, cancellationToken);
        var fileSize = new FileInfo(sourcePath).Length;
        var upload = VideoUpload.Create(
            tenantId,
            uploaderAccountId,
            "Legacy integration video",
            Path.GetFileName(sourcePath),
            fileSize,
            "video/mp4",
            "0123456789abcdef",
            "Integration teacher",
            uploadedAt);
        upload.SetStorageUploadId("legacy-storage-upload");
        upload.MarkCompleted(uploadedAt.AddSeconds(1));

        await UploadOriginalAsync(upload.ObjectKey, sourcePath, cancellationToken);
        var video = Video.Create(new VideoCreateInput(
            upload.VideoId,
            tenantId,
            upload.Title,
            uploaderAccountId,
            "Integration teacher",
            uploadedAt,
            upload.ObjectKey,
            fileSize,
            null));
        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            dbContext.Videos.Add(video);
            dbContext.VideoUploads.Add(upload);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
        }

        var legacyVideo = new TestVideo(upload.VideoId, tenantId, upload.ObjectKey, sourcePath, fileSize);
        videos.Add(legacyVideo);
        return legacyVideo;
    }

    public async Task<TestVideo> CreateDatabaseOnlyVideoAsync(long fileSize, CancellationToken cancellationToken)
    {
        var videoId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var originalObjectKey = $"{tenantId:D}/{videoId:D}/original";
        var video = CreateDomainVideo(videoId, tenantId, originalObjectKey, fileSize, DateTimeOffset.UtcNow);
        await AddVideoAsync(video, cancellationToken);
        var testVideo = new TestVideo(videoId, tenantId, originalObjectKey, string.Empty, fileSize);
        videos.Add(testVideo);
        return testVideo;
    }

    public async Task SeedReadyVideoAsync(TestVideo testVideo, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var video = await dbContext.Videos.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.VideoId == testVideo.VideoId, cancellationToken);
        video.MarkPreparing(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(5));
        video.MarkReady(
            video.PreparationLeaseId!.Value,
            20,
            1_024,
            new byte[45],
            MasterKeyId);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
    }

    public async Task<bool> ExecuteNextAsync(long freeDiskBytes, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IVideoPreparationWorkflow>().ExecuteNextAsync(
            WorkDirectory,
            freeDiskBytes,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30),
            cancellationToken);
    }

    public async Task MakePreparationEligibleAsync(Guid videoId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE media_access.videos SET next_preparation_at = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE video_id = {videoId}",
            cancellationToken);
    }

    public async Task ExpirePreparationLeaseAsync(Guid videoId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE media_access.videos SET preparation_lease_until = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE video_id = {videoId}",
            cancellationToken);
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE media_access.videos, media_access.video_uploads, media_access.outbox_messages CASCADE",
            cancellationToken);
        videos.Clear();
    }

    public async Task<IReadOnlyList<string>> ListObjectKeysAsync(string objectPrefix, CancellationToken cancellationToken)
    {
        using var client = CreateS3Client();
        var keys = new List<string>();
        string? continuationToken = null;
        do
        {
            var page = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = MediaIntegrationFixture.MinioBucketName,
                Prefix = $"media/{objectPrefix.TrimEnd('/')}/",
                ContinuationToken = continuationToken,
            }, cancellationToken);
            keys.AddRange(page.S3Objects?.Select(item => item.Key) ?? []);
            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return keys;
    }

    public async Task DownloadHlsTreeAsync(TestVideo video, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var client = CreateS3Client();
        var prefix = $"media/{video.TenantId:D}/{video.VideoId:D}/hls/";
        var keys = await ListObjectKeysAsync($"{video.TenantId:D}/{video.VideoId:D}/hls", cancellationToken);
        foreach (var key in keys)
        {
            var relativePath = key[prefix.Length..];
            var path = Path.Combine(destinationDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var response = await client.GetObjectAsync(
                MediaIntegrationFixture.MinioBucketName,
                key,
                cancellationToken);
            await using var output = File.Create(path);
            await response.ResponseStream.CopyToAsync(output, cancellationToken);
        }
    }

    public IAmazonS3 CreateAnonymousS3Client()
        => new AmazonS3Client(
            new AnonymousAWSCredentials(),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                ServiceURL = factory.MinioEndpoint,
            });

    public async Task UploadOriginalBytesAsync(string originalObjectKey, byte[] contents, CancellationToken cancellationToken)
    {
        using var client = CreateS3Client();
        await using var stream = new MemoryStream(contents, writable: false);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = MediaIntegrationFixture.MinioBucketName,
            Key = $"media/{originalObjectKey}",
            InputStream = stream,
            CannedACL = S3CannedACL.Private,
        }, cancellationToken);
    }

    public Task<FfmpegProcessResult> RunFfmpegAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunProcessAsync(FfmpegPath, arguments, cancellationToken, allowFailure: true);

    public async Task<IReadOnlyList<PublishedMessage>> PublishRepeatedlyAsync(
        OutboxMessage message,
        int times,
        CancellationToken cancellationToken)
    {
        await services.GetRequiredService<RabbitMqTopologyInitializer>().StartAsync(cancellationToken);
        var connectionProvider = services.GetRequiredService<RabbitMqConnectionProvider>();
        await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);
        var queue = await channel.QueueDeclareAsync(
            $"media.test.{Guid.CreateVersion7():N}",
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue.QueueName, "media.events", message.RoutingKey, cancellationToken: cancellationToken);

        var publisher = services.GetRequiredService<RabbitMqPublisher>();
        for (var attempt = 0; attempt < times; attempt++)
        {
            await publisher.PublishAsync(message, cancellationToken);
        }

        var delivered = new List<PublishedMessage>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (delivered.Count < times && DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken);
            if (result is null)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            delivered.Add(new PublishedMessage(
                result.BasicProperties.MessageId,
                result.RoutingKey,
                Encoding.UTF8.GetString(result.Body.Span)));
        }

        return delivered;
    }

    public byte[] UnprotectKey(Guid videoId, string keyId, byte[] encryptedKey)
        => services.GetRequiredService<IVideoKeyProtector>().Unprotect(videoId, keyId, encryptedKey);

    public async ValueTask DisposeAsync()
    {
        await using (var scope = services.CreateAsyncScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IMediaStoragePort>();
            foreach (var video in videos)
            {
                await storage.DeletePrefixAsync($"{video.TenantId:D}/{video.VideoId:D}", CancellationToken.None);
            }
        }

        await services.DisposeAsync();
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }

    private async Task AddVideoAsync(Video video, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        dbContext.Videos.Add(video);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
    }

    private async Task CreateSourceVideoAsync(
        string path,
        int width,
        int height,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        var size = $"{width}x{height}";
        await RunProcessAsync(
            FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-y",
                "-f", "lavfi", "-i", $"testsrc=size={size}:rate=10:duration={durationSeconds}",
                "-t", durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                path,
            ],
            cancellationToken);
    }

    private static async Task<string> CreateFfmpegWrapperAsync(
        string rootDirectory,
        string ffmpegPath,
        bool captureArguments,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The ffmpeg test wrapper requires Linux.");
        }

        var wrapperPath = Path.Combine(rootDirectory, "ffmpeg-wrapper");
        var capturePath = Path.Combine(rootDirectory, "ffmpeg-arguments.log");
        var script = new StringBuilder("#!/bin/sh\n");
        if (captureArguments)
        {
            script.Append("printf '%s\\n' \"$@\" >> '")
                .Append(EscapeShellPath(capturePath))
                .AppendLine("'");
            script.Append("printf '\\n' >> '")
                .Append(EscapeShellPath(capturePath))
                .AppendLine("'");
        }

        script.Append("exec '")
            .Append(EscapeShellPath(ffmpegPath))
            .AppendLine("' \"$@\"");
        await File.WriteAllTextAsync(wrapperPath, script.ToString(), cancellationToken);
        File.SetUnixFileMode(
            wrapperPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return wrapperPath;
    }

    private static string EscapeShellPath(string path)
        => path.Replace("'", "'\\''", StringComparison.Ordinal);

    private async Task UploadOriginalAsync(string originalObjectKey, string sourcePath, CancellationToken cancellationToken)
    {
        using var client = CreateS3Client();
        await using var stream = File.OpenRead(sourcePath);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = MediaIntegrationFixture.MinioBucketName,
            Key = $"media/{originalObjectKey}",
            InputStream = stream,
            CannedACL = S3CannedACL.Private,
        }, cancellationToken);
    }

    private static Video CreateDomainVideo(
        Guid videoId,
        Guid tenantId,
        string originalObjectKey,
        long fileSize,
        DateTimeOffset uploadedAt)
        => Video.Create(new VideoCreateInput(
            videoId,
            tenantId,
            "Synthetic integration video",
            Guid.CreateVersion7(),
            "Integration teacher",
            uploadedAt,
            originalObjectKey,
            fileSize,
            "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"));

    private IAmazonS3 CreateS3Client()
        => new AmazonS3Client(
            new BasicAWSCredentials(MediaIntegrationFixture.MinioAccessKey, MediaIntegrationFixture.MinioSecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                ServiceURL = factory.MinioEndpoint,
            });

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A canceled ffmpeg process is killed and awaited before propagating cancellation.")]
    private static async Task<FfmpegProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        var error = await standardError;
        _ = await standardOutput;
        if (!allowFailure)
        {
            AssertExitCode(process.ExitCode, error);
        }

        return new FfmpegProcessResult(process.ExitCode, error);
    }

    private static void AssertExitCode(int exitCode, string error)
    {
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {exitCode}: {error}");
        }
    }

    internal sealed record TestVideo(
        Guid VideoId,
        Guid TenantId,
        string OriginalObjectKey,
        string SourcePath,
        long FileSize);

    public sealed record TestOverrides(
        int StorageDownloadFailures = 0,
        string? TranscoderFfmpegPath = null,
        int Threads = 2,
        bool CaptureFfmpegArguments = false);

    private sealed class StorageFailureController(int remainingFailures)
    {
        private int failuresRemaining = remainingFailures;
        private int downloadAttemptCount;

        public int DownloadAttemptCount => Volatile.Read(ref downloadAttemptCount);

        public bool TryFailDownload()
        {
            Interlocked.Increment(ref downloadAttemptCount);
            while (true)
            {
                var remaining = Volatile.Read(ref failuresRemaining);
                if (remaining <= 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref failuresRemaining, remaining - 1, remaining) == remaining)
                {
                    return true;
                }
            }
        }
    }

    private sealed class FaultInjectingMediaStoragePort(
        S3MediaStorageAdapter inner,
        StorageFailureController failures) : IMediaStoragePort
    {
        public Task<string> InitiateMultipartUploadAsync(
            string objectKey,
            string contentType,
            CancellationToken cancellationToken)
            => inner.InitiateMultipartUploadAsync(objectKey, contentType, cancellationToken);

        public Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(
            string objectKey,
            string storageUploadId,
            CancellationToken cancellationToken)
            => inner.ListPartsAsync(objectKey, storageUploadId, cancellationToken);

        public Task<Uri> CreatePartUploadUriAsync(
            string objectKey,
            string storageUploadId,
            int partNumber,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
            => inner.CreatePartUploadUriAsync(objectKey, storageUploadId, partNumber, expiresAt, cancellationToken);

        public Task CompleteMultipartUploadAsync(
            string objectKey,
            string storageUploadId,
            IReadOnlyList<MediaUploadPart> parts,
            CancellationToken cancellationToken)
            => inner.CompleteMultipartUploadAsync(objectKey, storageUploadId, parts, cancellationToken);

        public Task AbortMultipartUploadAsync(
            string objectKey,
            string storageUploadId,
            CancellationToken cancellationToken)
            => inner.AbortMultipartUploadAsync(objectKey, storageUploadId, cancellationToken);

        public Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken)
        {
            if (failures.TryFailDownload())
            {
                throw new StorageUnavailableException();
            }

            return inner.DownloadObjectAsync(objectKey, destinationPath, cancellationToken);
        }

        public Task UploadDirectoryAsync(
            string sourceDirectory,
            string objectPrefix,
            CancellationToken cancellationToken)
            => inner.UploadDirectoryAsync(sourceDirectory, objectPrefix, cancellationToken);

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
            => inner.DeleteObjectAsync(objectKey, cancellationToken);

        public Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken)
            => inner.DeletePrefixAsync(objectPrefix, cancellationToken);
    }

    internal sealed record FfmpegProcessResult(int ExitCode, string StandardError);

    internal sealed record PublishedMessage(string? MessageId, string RoutingKey, string Body);

    internal sealed class CapturedLogProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> entries = new();

        public IReadOnlyCollection<string> Entries => entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new CapturedLogger(entries);

        public void Dispose()
        {
        }

        private sealed class CapturedLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}
