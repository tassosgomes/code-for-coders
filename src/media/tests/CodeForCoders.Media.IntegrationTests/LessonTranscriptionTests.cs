using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Data.CourseReferences;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class LessonTranscriptionTests(VideoLibraryApiFactory factory)
{
    private const string PrivateText = "Introdução <script>literal</script>";

    [Fact(DisplayName = nameof(EntitledStudentReadsCurrentTextWithoutOpeningPlaybackOrDisclosingIdentity))]
    public async Task EntitledStudentReadsCurrentTextWithoutOpeningPlaybackOrDisclosingIdentity()
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        using var client = test.Client(email: null); using var response = await ReadAsync(client, test.Lesson);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(test.Lesson, body.GetProperty("lessonId").GetGuid());
        Assert.Equal("ready", body.GetProperty("status").GetString());
        Assert.Equal("pt-BR", body.GetProperty("language").GetString());
        Assert.Equal(60_000, body.GetProperty("durationMs").GetInt64());
        Assert.Equal(PrivateText, body.GetProperty("segments")[0].GetProperty("text").GetString());
        Assert.False(body.TryGetProperty("videoId", out _)); Assert.False(body.TryGetProperty("email", out _));
        Assert.Equal(0, await test.CountSessionsAsync(TestContext.Current.CancellationToken));
    }

    [Theory(DisplayName = nameof(AuthorizationPrecedesContentAndFailsClosed))]
    [InlineData("missing", 404, "LESSON_NOT_AVAILABLE", false)]
    [InlineData("foreign", 404, "LESSON_NOT_AVAILABLE", false)]
    [InlineData("denied", 403, "ACCESS_DENIED", true)]
    [InlineData("unavailable", 503, "ACCESS_DECISION_UNAVAILABLE", true)]
    [InlineData("denied-pending", 403, "ACCESS_DENIED", true)]
    public async Task AuthorizationPrecedesContentAndFailsClosed(string scenario, int status, string code, bool called)
    {
        var test = await SeedAsync(scenario != "denied-pending");
        if (scenario != "denied-pending") await CurrentAsync(test);
        factory.PlaybackDecisions[test.Tenant] = scenario.StartsWith("denied", StringComparison.Ordinal) ? 403 : scenario == "unavailable" ? 503 : 200;
        using var client = scenario == "foreign" ? factory.CreateClient() : test.Client();
        if (scenario == "foreign") client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateStudentToken(Guid.CreateVersion7(), test.Student, null, "media"));
        using var response = await ReadAsync(client, scenario == "missing" ? Guid.CreateVersion7() : test.Lesson);
        Assert.Equal(status, (int)response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(json.TryGetProperty("segments", out _));
        Assert.Equal(called, factory.PlaybackDecisionCalls.ContainsKey(test.Tenant));
    }

    [Fact(DisplayName = nameof(VisitorCannotReadAndDoesNotConsultEnrollment))]
    public async Task VisitorCannotReadAndDoesNotConsultEnrollment()
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        using var client = factory.CreateClient(); using var response = await ReadAsync(client, test.Lesson);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(PrivateText, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(factory.PlaybackDecisionCalls.ContainsKey(test.Tenant));
    }

    [Theory(DisplayName = nameof(NoCurrentTranscriptionIsNeutralAfterPositiveAccessDecision))]
    [InlineData("none")]
    [InlineData("queued")]
    [InlineData("failed")]
    [InlineData("pending-video")]
    [InlineData("disabled")]
    public async Task NoCurrentTranscriptionIsNeutralAfterPositiveAccessDecision(string state)
    {
        var test = await SeedAsync(state != "pending-video");
        if (state == "disabled") await CurrentAsync(test);
        else if (state is "queued" or "failed") await AttemptAsync(test, state);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<TranscriptionOptions>(options => options.Enabled = state != "disabled")));
        using var client = withHost(host);
        using var response = await ReadAsync(client, test.Lesson);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("unavailable", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("revision").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("language").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("durationMs").ValueKind);
        Assert.Empty(json.GetProperty("segments").EnumerateArray());
        Assert.True(factory.PlaybackDecisionCalls.ContainsKey(test.Tenant));

        HttpClient withHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> selected)
        {
            var client = selected.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateStudentToken(test.Tenant, test.Student, null, "media"));
            return client;
        }
    }

    [Theory(DisplayName = nameof(RestartKeepsCurrentTextReadable))]
    [InlineData("queued")]
    [InlineData("failed")]
    public async Task RestartKeepsCurrentTextReadable(string state)
    {
        var test = await SeedAsync(); await CurrentAsync(test); await AttemptAsync(test, state);
        using var client = test.Client(); using var response = await ReadAsync(client, test.Lesson);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("ready", json.GetProperty("status").GetString());
        Assert.Equal(PrivateText, json.GetProperty("segments")[0].GetProperty("text").GetString());
    }

    [Fact(DisplayName = nameof(RepublishingLessonWithAnotherVideoReadsTheNewVideoText))]
    public async Task RepublishingLessonWithAnotherVideoReadsTheNewVideoText()
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        var replacement = await SeedAsync(); await CurrentAsync(replacement, "New published content");
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            // Move the replacement into the same school, then replace the live lesson reference.
            await db.Videos.IgnoreQueryFilters().Where(v => v.VideoId == replacement.VideoId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.TenantId, test.Tenant), TestContext.Current.CancellationToken);
            await db.Transcriptions.IgnoreQueryFilters().Where(t => t.VideoId == replacement.VideoId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.TenantId, test.Tenant), TestContext.Current.CancellationToken);
            await db.TranscriptionContents.IgnoreQueryFilters().Where(t => t.TenantId == replacement.Tenant)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.TenantId, test.Tenant), TestContext.Current.CancellationToken);
            await db.CourseVideoReferences.Where(r => r.LessonId == test.Lesson)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.VideoId, replacement.VideoId), TestContext.Current.CancellationToken);
        }
        using var client = test.Client(); using var response = await ReadAsync(client, test.Lesson);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("New published content", json.GetProperty("segments")[0].GetProperty("text").GetString());
    }

    [Fact(DisplayName = nameof(PlaybackAndTranscriptionReuseTheShortAccessDecision))]
    public async Task PlaybackAndTranscriptionReuseTheShortAccessDecision()
    {
        var test = await SeedAsync(); await CurrentAsync(test); using var client = test.Client();
        using var transcription = await ReadAsync(client, test.Lesson);
        using var playback = await client.PostAsync($"/internal/v1/lessons/{test.Lesson:D}/playback-sessions", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, transcription.StatusCode); Assert.Equal(HttpStatusCode.Created, playback.StatusCode);
        Assert.Equal(1, factory.PlaybackDecisionCalls[test.Tenant]);
    }

    private async Task<PlaybackTestContext> SeedAsync(bool ready = true)
    {
        var test = new PlaybackTestContext(factory);
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var video = Video.Create(new(test.VideoId, test.Tenant, "Lesson", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow,
            $"{test.Tenant:D}/{test.VideoId:D}/original", 1024, null));
        if (ready) { var lease = Guid.CreateVersion7(); video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5)); video.MarkReady(lease, 60, 1024, new byte[32], "test-key"); }
        db.Videos.Add(video); db.CourseVideoReferences.Add(new CourseVideoReference { TenantId = test.Tenant, CourseId = test.Course, LessonId = test.Lesson, VideoId = test.VideoId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return test;
    }

    private async Task CurrentAsync(PlaybackTestContext test, string text = PrivateText)
    {
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var attempt = Transcription.Create(new(test.Tenant, test.VideoId, "requested", test.Student, DateTimeOffset.UtcNow));
        db.Transcriptions.Add(attempt); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'working' WHERE transcription_id = {attempt.TranscriptionId}", TestContext.Current.CancellationToken);
        await db.Entry(attempt).ReloadAsync(TestContext.Current.CancellationToken);
        var content = TranscriptionContent.Create(new(attempt.TranscriptionId, test.Tenant, "pt-BR", 60_000, [new(0, 12_000, text), new(12_000, 60_000, "Second segment")]));
        attempt.MarkReady(content, DateTimeOffset.UtcNow); db.TranscriptionContents.Add(content);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AttemptAsync(PlaybackTestContext test, string state)
    {
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var attempt = Transcription.Create(new(test.Tenant, test.VideoId, "requested", test.Student, DateTimeOffset.UtcNow));
        if (state == "failed") attempt.MarkFailed("processing-failed");
        db.Transcriptions.Add(attempt); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
    private static Task<HttpResponseMessage> ReadAsync(HttpClient client, Guid lesson)
        => client.GetAsync($"/internal/v1/lessons/{lesson:D}/transcription", TestContext.Current.CancellationToken);
}
