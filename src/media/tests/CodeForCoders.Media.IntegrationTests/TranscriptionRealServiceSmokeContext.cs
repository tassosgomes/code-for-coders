using System.Security.Cryptography;
using Amazon.S3.Model;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Transcriptions.ProcessTranscription;
using CodeForCoders.Media.Application.UseCases.Videos.GetVideoTranscription;
using CodeForCoders.Media.Application.UseCases.Videos.PrepareVideo;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Data.Transcriptions;
using CodeForCoders.Media.Infra.Data.Videos;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace CodeForCoders.Media.IntegrationTests;

internal sealed class TranscriptionRealServiceSmokeContext : IAsyncDisposable
{
    private readonly ServiceProvider services;
    private readonly string root = Path.Combine(Path.GetTempPath(), $"media-real-smoke-{Guid.CreateVersion7():N}");
    private bool databaseInitialized;
    public Guid TenantId { get; } = Guid.CreateVersion7();
    public Guid VideoId { get; } = Guid.CreateVersion7();
    public string VideoPrefix => $"{TenantId:D}/{VideoId:D}";
    public string WorkDirectory => Path.Combine(root, "work");

    public TranscriptionRealServiceSmokeContext(TranscriptionRealServiceSmokeSettings settings, (string Ffmpeg, string Ffprobe) tools)
    {
        var registrations = new ServiceCollection();
        // No providers or HTTP loggers: exceptions may contain signed URLs and upstream payloads.
        registrations.AddLogging();
        registrations.AddSingleton(TimeProvider.System);
        registrations.AddScoped<ITenantContext, TenantContext>();
        registrations.AddDbContext<MediaDbContext>(options => options.UseNpgsql(settings.ConnectionString,
            database => database.MigrationsHistoryTable("__ef_migrations_history", MediaSchema.Name)));
        registrations.AddScoped<IUnitOfWork, MediaUnitOfWork>();
        registrations.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        registrations.AddScoped<IVideoPreparationRepository, VideoPreparationRepository>();
        registrations.AddScoped<IVideoTranscriptionRepository, VideoTranscriptionRepository>();
        registrations.AddScoped<ITranscriptionWorkerRepository, TranscriptionWorkerRepository>();
        registrations.AddScoped<IVideoPreparationWorkflow, PrepareVideo>();
        registrations.AddScoped<ITranscriptionWorkflow, ProcessTranscription>();
        registrations.AddScoped<ITranscriptionAudioExtractor, FfmpegTranscriptionAudioExtractor>();
        registrations.AddSingleton<IVideoTranscoder, FfmpegVideoTranscoder>();
        registrations.AddSingleton<IVideoKeyProtector, AesVideoKeyProtector>();
        registrations.AddSingleton<S3MediaClientPair>();
        registrations.AddScoped<IMediaStoragePort, S3MediaStorageAdapter>();
        registrations.AddScoped<IPlaybackPlaylistReader, S3MediaStorageAdapter>();
        registrations.AddScoped<IVideoQueries, VideoQueries>();
        registrations.AddScoped<ITranscriptionQueries, TranscriptionQueries>();
        registrations.AddScoped<IGetVideoTranscription, GetVideoTranscription>();
        registrations.Configure<TranscriptionOptions>(options =>
        {
            options.Enabled = true;
            options.InitialPollSeconds = options.IntermediatePollSeconds = options.LongPollSeconds = 15;
        });
        registrations.Configure<VideoPreparationOptions>(options =>
        {
            options.WorkDirectory = WorkDirectory;
            options.FfmpegPath = tools.Ffmpeg;
            options.FfprobePath = tools.Ffprobe;
            options.Threads = 1;
            options.MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            options.MasterKeyId = "real-smoke-ephemeral";
        });
        registrations.Configure<AwsMediaOptions>(options =>
        {
            options.BucketName = TranscriptionRealServiceSmokeSettings.BucketName;
            options.ObjectKeyPrefix = "media";
            options.EndpointInternal = options.EndpointPublic = options.EndpointTranscription = settings.S3Endpoint;
            options.AccessKeyId = settings.S3AccessKey;
            options.SecretAccessKey = settings.S3SecretKey;
            options.ForcePathStyle = true;
        });
        registrations.Configure<TranscriptionServiceOptions>(options =>
        {
            options.BaseAddress = TranscriptionRealServiceSmokeSettings.ServiceAddress;
            options.ApiKey = settings.ApiKey;
        });
        registrations.AddHttpClient<ITranscriptionServiceClient, TranscriptionServiceClient>().RemoveAllLoggers()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        await WithScopeAsync(async provider =>
        {
            var db = provider.GetRequiredService<MediaDbContext>();
            await db.Database.MigrateAsync(token);
            if (await db.Videos.IgnoreQueryFilters().AnyAsync(token) || await db.Transcriptions.IgnoreQueryFilters().AnyAsync(token))
                throw new InvalidOperationException("The exclusive smoke database must contain no videos or transcription attempts.");
            databaseInitialized = true;
            return true;
        });
        Directory.CreateDirectory(WorkDirectory);
    }

    public async Task UploadAsync(string path, CancellationToken token)
    {
        await services.GetRequiredService<S3MediaClientPair>().Internal.PutObjectAsync(new PutObjectRequest
        {
            BucketName = TranscriptionRealServiceSmokeSettings.BucketName,
            Key = $"media/{VideoPrefix}/original",
            FilePath = path,
            ContentType = "video/mp4",
        }, token);
        await WithScopeAsync(async provider =>
        {
            var video = Video.Create(new(VideoId, TenantId, "Real transcription smoke", Guid.CreateVersion7(),
                "Smoke teacher", DateTimeOffset.UtcNow, VideoPrefix + "/original", new FileInfo(path).Length, null));
            provider.GetRequiredService<MediaDbContext>().Videos.Add(video);
            await provider.GetRequiredService<IUnitOfWork>().CommitAsync(token);
            return true;
        });
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = services.CreateAsyncScope();
        ((TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>()).Set(TenantId);
        return await action(scope.ServiceProvider);
    }

    public async Task<bool> IsAudioPrefixEmptyAsync(CancellationToken token)
    {
        var response = await services.GetRequiredService<S3MediaClientPair>().Internal.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = TranscriptionRealServiceSmokeSettings.BucketName,
            Prefix = $"media/{VideoPrefix}/transcription/",
            MaxKeys = 1,
        }, token);
        return response.KeyCount == 0;
    }

    public async ValueTask DisposeAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await WithScopeAsync(async provider =>
            {
                await provider.GetRequiredService<IMediaStoragePort>().DeletePrefixAsync(VideoPrefix, cleanup.Token);
                if (!databaseInitialized) return true;
                var db = provider.GetRequiredService<MediaDbContext>();
                await db.TranscriptionEvents.IgnoreQueryFilters().Where(row => row.TenantId == TenantId).ExecuteDeleteAsync(cleanup.Token);
                await db.TranscriptionAudioCleanups.IgnoreQueryFilters().Where(row => row.TenantId == TenantId).ExecuteDeleteAsync(cleanup.Token);
                await db.Videos.IgnoreQueryFilters().Where(row => row.VideoId == VideoId && row.TenantId == TenantId).ExecuteDeleteAsync(cleanup.Token);
                await db.OutboxMessages.IgnoreQueryFilters().Where(row => row.TenantId == TenantId).ExecuteDeleteAsync(cleanup.Token);
                return true;
            });
        }
        finally
        {
            await services.DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
