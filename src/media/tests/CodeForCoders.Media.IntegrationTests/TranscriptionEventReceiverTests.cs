using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class TranscriptionEventReceiverTests(VideoLibraryApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/webhooks/v1/transcription-events";
    private const string JobId = "trn_test";
    private static readonly byte[] CurrentKey = RandomNumberGenerator.GetBytes(24);
    private static readonly byte[] PreviousKey = RandomNumberGenerator.GetBytes(24);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var connection in new[] { Db(scope).Database.GetConnectionString()!, factory.VideoPreparationConnectionString })
        {
            await using var source = Npgsql.NpgsqlDataSource.Create(connection);
            await using var command = source.CreateCommand("TRUNCATE media_access.video_transcription_events");
            await command.ExecuteNonQueryAsync(Token);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ValidSignatureWithCurrentSecretSignalsTheWaitingAttempt()
    {
        await using var host = Host(); var (tenant, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var attempt = await AttemptAsync(host, id);
        Assert.NotNull(attempt.SignaledAt); Assert.True(attempt.NextActionAt <= DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal("waiting", attempt.Status);
        Assert.Equal(tenant, (await EventsAsync(host)).Single().TenantId);
    }

    [Fact]
    public async Task ValidSignatureWithThePreviousSecretSignalsTheAttemptDuringRotation()
    {
        await using var host = Host(rotation: true); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, key: PreviousKey);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await AttemptAsync(host, id)).SignaledAt);
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("working")]
    public async Task AttemptThatHasNotRecordedItsJobYetIsSignaled(string status)
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, status, null);
        using var response = await PostEventAsync(host, id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await AttemptAsync(host, id)).SignaledAt);
    }

    [Fact]
    public async Task InvalidSignatureIsRefusedWithoutEchoingTheBodyOrChangingAnything()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, key: RandomNumberGenerator.GetBytes(24));
        await ProblemAsync(response, HttpStatusCode.Unauthorized, "SIGNATURE_INVALID", id.ToString("D"), JobId);
        Assert.Null((await AttemptAsync(host, id)).SignaledAt); Assert.Empty(await EventsAsync(host));
    }

    [Theory]
    [InlineData("webhook-id")]
    [InlineData("webhook-timestamp")]
    [InlineData("webhook-signature")]
    public async Task MissingHeaderIsRefusedAsUnauthenticated(string header)
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, omit: header);
        await ProblemAsync(response, HttpStatusCode.Unauthorized, "SIGNATURE_INVALID");
        Assert.Null((await AttemptAsync(host, id)).SignaledAt); Assert.Empty(await EventsAsync(host));
    }

    [Theory]
    [InlineData(-310)]
    [InlineData(310)]
    public async Task TimestampOutsideTheWindowIsRefusedEvenWithAValidSignature(int offsetSeconds)
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, timestamp: DateTimeOffset.UtcNow.AddSeconds(offsetSeconds).ToUnixTimeSeconds());
        await ProblemAsync(response, HttpStatusCode.Unauthorized, "SIGNATURE_INVALID");
        Assert.Null((await AttemptAsync(host, id)).SignaledAt);
    }

    [Fact]
    public async Task RedeliveredEventIsAcknowledgedWithoutASecondEffect()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId); var eventId = NewEventId();
        using var first = await PostEventAsync(host, id, eventId: eventId); Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await using (var scope = Scope(host)) await Db(scope).Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET signaled_at = NULL WHERE transcription_id = {id}", Token);
        using var second = await PostEventAsync(host, id, eventId: eventId);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Null((await AttemptAsync(host, id)).SignaledAt); Assert.Single(await EventsAsync(host));
    }

    [Fact]
    public async Task UnknownReferenceIsAcknowledgedAndRecordedWithoutEffect()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await AttemptAsync(host, id)).SignaledAt); Assert.Single(await EventsAsync(host));
    }

    [Fact]
    public async Task EventWithoutClientReferenceIsAcknowledgedWithoutEffect()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, reference: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await AttemptAsync(host, id)).SignaledAt);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("failed")]
    public async Task EndedAttemptIsAcknowledgedWithoutEffect(string status)
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, status, JobId);
        using var response = await PostEventAsync(host, id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var attempt = await AttemptAsync(host, id); Assert.Null(attempt.SignaledAt); Assert.Equal(status, attempt.Status);
    }

    [Fact]
    public async Task EventOfAnotherJobIsAcknowledgedWithoutEffect()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, jobId: "trn_other");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await AttemptAsync(host, id)).SignaledAt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"eventId":"other","jobId":"trn_test","status":"completed"}""")]
    [InlineData("""{"jobId":"trn_test","status":"completed"}""")]
    [InlineData("""{"eventId":"evt","status":"completed"}""")]
    [InlineData("""{"eventId":"evt","jobId":"trn_test","status":"queued"}""")]
    public async Task SignedButMalformedEventIsRejectedWithoutEchoingIt(string body)
    {
        await using var host = Host();
        using var response = await PostRawAsync(host, "evt", body);
        await ProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST", "trn_test", "not json");
        Assert.Empty(await EventsAsync(host));
    }

    [Fact]
    public async Task BodyOverTheLimitIsRejected()
    {
        await using var host = Host();
        using var response = await PostRawAsync(host, "evt", new string('x', 16 * 1024 + 1));
        await ProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
    }

    [Fact]
    public async Task StorageFailureAnswersTemporarilyUnavailableNeverGone()
    {
        await using var host = Host(services =>
        {
            services.RemoveAll<ITranscriptionEventRepository>(); services.AddScoped<ITranscriptionEventRepository, UnavailableEventRepository>();
        });
        using var response = await PostEventAsync(host, Guid.CreateVersion7());
        await ProblemAsync(response, HttpStatusCode.ServiceUnavailable, "TEMPORARILY_UNAVAILABLE");
        Assert.NotEqual(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task WithoutAConfiguredSecretTheRouteDoesNotExist()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(Route, new StringContent("{}", Encoding.UTF8, "application/json"), Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompletedNoticeNeverTurnsTheAttemptIntoReadyOrWritesText()
    {
        await using var host = Host(); var (_, id) = await SeedAsync(host, "waiting", JobId);
        using var response = await PostEventAsync(host, id, status: "completed");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("waiting", (await AttemptAsync(host, id)).Status);
        await using var scope = Scope(host); Assert.Empty(await Db(scope).TranscriptionContents.IgnoreQueryFilters().ToArrayAsync(Token));
    }

    [Fact]
    public async Task WorkerPollsRightAfterTheNoticeInsteadOfWaitingForTheCadence()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Status = "completed";
        await using var worker = await CreateWorkerAsync(server);
        await using var host = Host(connection: factory.VideoPreparationConnectionString);
        var (_, id) = await SeedAsync(host, "waiting", JobId, nextActionIn: TimeSpan.FromHours(1));
        Assert.False(await ExecuteAsync(worker)); Assert.Equal("waiting", (await AttemptAsync(host, id)).Status);
        using var response = await PostEventAsync(host, id);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await ExecuteAsync(worker));
        var ready = await AttemptAsync(host, id); Assert.Equal("ready", ready.Status); Assert.True(ready.IsCurrent);
        await using var scope = Scope(host); Assert.NotNull(await Db(scope).TranscriptionContents.IgnoreQueryFilters().SingleOrDefaultAsync(row => row.TranscriptionId == id, Token));
    }

    [Fact]
    public async Task NoticeIsOnlyATriggerWhenTheServiceDoesNotConfirmCompletion()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token); server.Status = "processing";
        await using var worker = await CreateWorkerAsync(server);
        await using var host = Host(connection: factory.VideoPreparationConnectionString);
        var (_, id) = await SeedAsync(host, "waiting", JobId, nextActionIn: TimeSpan.FromHours(1));
        using var response = await PostEventAsync(host, id, status: "completed");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await ExecuteAsync(worker));
        var attempt = await AttemptAsync(host, id);
        Assert.Equal("waiting", attempt.Status); Assert.Null(attempt.SignaledAt); Assert.True(attempt.NextActionAt > DateTimeOffset.UtcNow.AddSeconds(10));
        await using var scope = Scope(host); Assert.Empty(await Db(scope).TranscriptionContents.IgnoreQueryFilters().ToArrayAsync(Token));
    }

    [Fact]
    public async Task MaintenanceDeletesOnlyOldNotices()
    {
        await using var server = await TranscriptionHttpSimulator.StartAsync(Token);
        await using var worker = await CreateWorkerAsync(server);
        await using var host = Host(connection: factory.VideoPreparationConnectionString);
        await using (var scope = Scope(host))
            await Db(scope).Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO media_access.video_transcription_events (event_id, tenant_id, received_at) VALUES
                ('old', {Guid.Empty}, {DateTimeOffset.UtcNow.AddDays(-8)}), ('recent', {Guid.Empty}, {DateTimeOffset.UtcNow.AddDays(-1)})
                """, Token);
        await worker.WithScopeAsync(async provider => { await provider.GetRequiredService<ITranscriptionWorkflow>().MaintainAsync(worker.WorkDirectory, Token); return true; });
        Assert.Equal(["recent"], (await EventsAsync(host)).Select(row => row.EventId));
    }

    private static string SecretOf(byte[] key) => "whsec_" + Convert.ToBase64String(key);
    private static string NewEventId() => "evt_" + Guid.CreateVersion7().ToString("N");

    private WebApplicationFactory<Program> Host(Action<IServiceCollection>? services = null, bool rotation = false, string? connection = null)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Transcription:Webhook:Secrets:0", SecretOf(CurrentKey));
            if (rotation) builder.UseSetting("Transcription:Webhook:Secrets:1", SecretOf(PreviousKey));
            if (connection is not null) builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            if (services is not null) builder.ConfigureTestServices(services);
        });

    private async Task<(Guid Tenant, Guid Id)> SeedAsync(WebApplicationFactory<Program> host, string status, string? jobId, TimeSpan? nextActionIn = null)
    {
        var video = Video.Create(Guid.CreateVersion7(), "Lesson", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5)); video.MarkReady(lease, 10, 1024, new byte[32], "test-key");
        var attempt = Transcription.Create(new(video.TenantId, video.VideoId, "requested", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        await using var scope = Scope(host, video.TenantId); var db = Db(scope);
        db.Videos.Add(video); db.Transcriptions.Add(attempt); await db.SaveChangesAsync(Token);
        var now = DateTimeOffset.UtcNow; var next = now.Add(nextActionIn ?? TimeSpan.FromMinutes(10));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE media_access.video_transcriptions SET status = {status}, whisper_job_id = {jobId}::text, next_action_at = {next},
                submitted_at = {(jobId is null ? (DateTimeOffset?)null : now)}::timestamptz, deadline_at = {(jobId is null ? (DateTimeOffset?)null : now.AddHours(30))}::timestamptz,
                failure_reason = {(status == "failed" ? "processing-failed" : null)}::text
            WHERE transcription_id = {attempt.TranscriptionId}
            """, Token);
        return (video.TenantId, attempt.TranscriptionId);
    }

    private static async Task<Transcription> AttemptAsync(WebApplicationFactory<Program> host, Guid id)
    {
        await using var scope = Scope(host);
        return await Db(scope).Transcriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.TranscriptionId == id, Token);
    }

    private static async Task<TranscriptionEvent[]> EventsAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = Scope(host);
        return await Db(scope).TranscriptionEvents.IgnoreQueryFilters().AsNoTracking().ToArrayAsync(Token);
    }

    private static AsyncServiceScope Scope(WebApplicationFactory<Program> host, Guid? tenant = null)
    {
        var scope = host.Services.CreateAsyncScope();
        if (tenant is not null) scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant.Value);
        return scope;
    }

    private static MediaDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<MediaDbContext>();

    private static Task<HttpResponseMessage> PostEventAsync(WebApplicationFactory<Program> host, Guid? reference, string? eventId = null,
        string jobId = JobId, string status = "completed", byte[]? key = null, long? timestamp = null, string? omit = null)
    {
        eventId ??= NewEventId();
        var body = JsonSerializer.Serialize(reference is null
            ? new Dictionary<string, string> { ["eventId"] = eventId, ["jobId"] = jobId, ["status"] = status }
            : new Dictionary<string, string> { ["eventId"] = eventId, ["jobId"] = jobId, ["status"] = status, ["clientReference"] = reference.Value.ToString("D") });
        return PostRawAsync(host, eventId, body, key, timestamp, omit);
    }

    private static async Task<HttpResponseMessage> PostRawAsync(WebApplicationFactory<Program> host, string eventId, string body,
        byte[]? key = null, long? timestamp = null, string? omit = null)
    {
        var stamp = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var signature = "v1," + Convert.ToBase64String(HMACSHA256.HashData(key ?? CurrentKey, Encoding.UTF8.GetBytes($"{eventId}.{stamp}.{body}")));
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (omit != "webhook-id") request.Headers.TryAddWithoutValidation("webhook-id", eventId);
        if (omit != "webhook-timestamp") request.Headers.TryAddWithoutValidation("webhook-timestamp", stamp);
        if (omit != "webhook-signature") request.Headers.TryAddWithoutValidation("webhook-signature", signature);
        return await client.SendAsync(request, Token);
    }

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code, params string[] mustNotEcho)
    {
        Assert.Equal(status, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Token); using var json = JsonDocument.Parse(text);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        foreach (var value in mustNotEcho) Assert.DoesNotContain(value, text, StringComparison.Ordinal);
    }

    private Task<VideoPreparationTestContext> CreateWorkerAsync(TranscriptionHttpSimulator server)
        => VideoPreparationTestContext.CreateAsync(factory, Token, new(TranscriptionServiceAddress: server.BaseAddress, TranscriptionEnabled: true));

    private static Task<bool> ExecuteAsync(VideoPreparationTestContext worker)
        => worker.WithScopeAsync(provider => provider.GetRequiredService<ITranscriptionWorkflow>().ExecuteNextAsync(new(worker.WorkDirectory, long.MaxValue / 4), Token));

    private sealed class UnavailableEventRepository : ITranscriptionEventRepository
    {
        public Task<bool> RecordAsync(TranscriptionNotification notification, Func<Transcription?, CancellationToken, Task> change, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
