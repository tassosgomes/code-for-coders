using CodeForCoders.Media.Api.Extensions;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class TranscriptionWorkerCompositionTests(VideoLibraryApiFactory factory)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WorkerHostStartsWithRealRegistrationsAndConsumesAnAttempt()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        var directory = Path.Combine(Path.GetTempPath(), $"transcription-host-{Guid.CreateVersion7():N}");
        await using var app = Build(server, directory, true);
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE media_access.videos, media_access.outbox_messages CASCADE", Token);
            var tenantId = Guid.CreateVersion7(); var videoId = Guid.CreateVersion7();
            var video = Video.Create(new(videoId, tenantId, "Worker composition", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow,
                $"{tenantId:D}/{videoId:D}/original", 100, null));
            var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
            video.MarkReady(lease, 7201, 100, new byte[45], "playback-test");
            var attempt = Transcription.Create(new(tenantId, videoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
            db.Videos.Add(video); db.Transcriptions.Add(attempt); await db.SaveChangesAsync(Token);
            Assert.IsType<CodeForCoders.Media.Infra.Data.Adapters.S3MediaStorageAdapter>(scope.ServiceProvider.GetRequiredService<IMediaStoragePort>());
            Assert.IsType<TranscriptionServiceClient>(scope.ServiceProvider.GetRequiredService<ITranscriptionServiceClient>());
            Assert.IsType<FfmpegTranscriptionAudioExtractor>(scope.ServiceProvider.GetRequiredService<ITranscriptionAudioExtractor>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITranscriptionWorkflow>());
            Assert.Contains(app.Services.GetServices<IHostedService>(), service => service is TranscriptionWorker);
            await app.StartAsync(Token);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
            while (true)
            {
                var current = await db.Transcriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.TranscriptionId == attempt.TranscriptionId, deadline.Token);
                if (current.Status == "failed") { Assert.Equal("duration-limit", current.FailureReason); break; }
                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token);
            }
            await app.StopAsync(Token);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task DisabledTranscriptionStartsWorkerWithoutServiceCredentialOrTranscriptionLoop()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        var directory = Path.Combine(Path.GetTempPath(), $"transcription-disabled-{Guid.CreateVersion7():N}");
        await using var app = Build(server, directory, false);
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Database.ExecuteSqlRawAsync("TRUNCATE media_access.videos, media_access.outbox_messages CASCADE", Token);
            Assert.DoesNotContain(app.Services.GetServices<IHostedService>(), service => service is TranscriptionWorker);
            Assert.Null(scope.ServiceProvider.GetService<ITranscriptionServiceClient>());
            Assert.Null(scope.ServiceProvider.GetService<ITranscriptionWorkflow>());
            await app.StartAsync(Token); await app.StopAsync(Token);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact(DisplayName = nameof(EnabledTranscriptionRejectsEmptyServiceCredentialAtStartup))]
    [Trait("Layer", "Media transcription worker - Integration")]
    public async Task EnabledTranscriptionRejectsEmptyServiceCredentialAtStartup()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        var directory = Path.Combine(Path.GetTempPath(), $"transcription-missing-credential-{Guid.CreateVersion7():N}");
        await using var app = Build(server, directory, true);
        app.Configuration["Transcription:Service:ApiKey"] = string.Empty;
        try
        {
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(Token));
            Assert.Equal(typeof(TranscriptionServiceOptions), exception.OptionsType);
            Assert.Contains("Transcription service settings are invalid.", exception.Failures);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private WebApplication Build(TranscriptionHttpSimulator server, string directory, bool enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "IntegrationTest",
            ContentRootPath = ApiDirectory(),
        });
        builder.Host.UseDefaultServiceProvider(settings =>
        {
            settings.ValidateScopes = true;
            settings.ValidateOnBuild = true;
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Media:Role"] = "worker",
            ["ConnectionStrings:DefaultConnection"] = factory.VideoPreparationConnectionString,
            ["Preparation:MasterKey"] = factory.PlaybackMasterKey,
            ["Preparation:MasterKeyId"] = "playback-test",
            ["Preparation:WorkDirectory"] = directory,
            ["Transcription:Enabled"] = enabled.ToString(),
            ["Transcription:PollingIntervalSeconds"] = "1",
            ["Transcription:Service:BaseAddress"] = server.BaseAddress,
            ["Transcription:Service:ApiKey"] = enabled ? TranscriptionHttpSimulator.ApiKey : "",
            ["AwsMedia:EndpointInternal"] = factory.MinioEndpoint,
            ["AwsMedia:EndpointPublic"] = factory.MinioEndpoint,
            ["AwsMedia:EndpointTranscription"] = factory.MinioEndpoint,
            ["AwsMedia:BucketName"] = MediaIntegrationFixture.MinioBucketName,
            ["AwsMedia:AccessKeyId"] = MediaIntegrationFixture.MinioAccessKey,
            ["AwsMedia:SecretAccessKey"] = MediaIntegrationFixture.MinioSecretKey,
            ["AwsMedia:ForcePathStyle"] = "true",
            ["RabbitMq:Host"] = factory.RabbitMqEndpoint.Host,
            ["RabbitMq:Port"] = factory.RabbitMqEndpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMq:Username"] = "code_for_coders",
            ["RabbitMq:Password"] = "code_for_coders",
        });
        builder.AddMediaConfiguration();
        return builder.Build();
    }

    private static string ApiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src", "media", "src", "CodeForCoders.Media.Api");
    }
}
