using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class PlaybackCaptionsTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(ValidSessionReadsEscapedVttWithPrivateNoStore))]
    public async Task ValidSessionReadsEscapedVttWithPrivateNoStore()
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        using var client = test.Client(); var session = await test.OpenAsync(client, TestContext.Current.CancellationToken);
        using var response = await ReadAsync(client, session);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/vtt; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("WEBVTT\n\n", body); Assert.Contains("Introduction &amp; &lt;script&gt;", body);
        Assert.DoesNotContain(test.Student.ToString(), body); Assert.DoesNotContain("student@example.com", body);
    }

    [Theory(DisplayName = nameof(UnknownOrForeignSessionsAreIndistinguishable))]
    [InlineData("unknown")]
    [InlineData("student")]
    [InlineData("school")]
    public async Task UnknownOrForeignSessionsAreIndistinguishable(string scenario)
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        using var owner = test.Client(); var session = await test.OpenAsync(owner, TestContext.Current.CancellationToken);
        using var client = scenario == "student" ? test.Client(Guid.CreateVersion7()) : test.Client();
        if (scenario == "school") client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateStudentToken(Guid.CreateVersion7(), test.Student, null, "media"));
        await AssertProblemAsync(client, scenario == "unknown" ? Guid.CreateVersion7() : session, 404, "PLAYBACK_SESSION_NOT_FOUND");
    }

    [Fact(DisplayName = nameof(ExpiredSessionCannotReadCaptions))]
    public async Task ExpiredSessionCannotReadCaptions()
    {
        var test = await SeedAsync(); await CurrentAsync(test);
        using var client = test.Client(); var session = await test.OpenAsync(client, TestContext.Current.CancellationToken);
        await test.ExpireAsync(session, TestContext.Current.CancellationToken);
        await AssertProblemAsync(client, session, 410, "PLAYBACK_SESSION_EXPIRED");
    }

    [Fact(DisplayName = nameof(SessionWithoutCurrentTranscriptReturnsCaptionsNotAvailable))]
    public async Task SessionWithoutCurrentTranscriptReturnsCaptionsNotAvailable()
    {
        var test = await SeedAsync(); using var client = test.Client();
        var session = await test.OpenAsync(client, TestContext.Current.CancellationToken);
        await AssertProblemAsync(client, session, 404, "CAPTIONS_NOT_AVAILABLE");
    }

    [Fact(DisplayName = nameof(CaptionsNeverConsultEnrollmentAgain))]
    public async Task CaptionsNeverConsultEnrollmentAgain()
    {
        var test = await SeedAsync(); await CurrentAsync(test); using var client = test.Client();
        var session = await test.OpenAsync(client, TestContext.Current.CancellationToken);
        factory.PlaybackDecisions[test.Tenant] = 403;
        var calls = factory.PlaybackDecisionCalls[test.Tenant];
        using var first = await ReadAsync(client, session); using var second = await ReadAsync(client, session);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(calls, factory.PlaybackDecisionCalls[test.Tenant]);
    }

    [Fact(DisplayName = nameof(ReprocessingKeepsCurrentCaptions))]
    public async Task ReprocessingKeepsCurrentCaptions()
    {
        var test = await SeedAsync(); await CurrentAsync(test); using var client = test.Client();
        var session = await test.OpenAsync(client, TestContext.Current.CancellationToken);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.Transcriptions.Add(Transcription.Create(new(test.Tenant, test.VideoId, "requested", test.Student, DateTimeOffset.UtcNow)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var response = await ReadAsync(client, session);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Introduction", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(VisitorCannotReadCaptionText))]
    public async Task VisitorCannotReadCaptionText()
    {
        var test = await SeedAsync(); await CurrentAsync(test); using var owner = test.Client();
        var session = await test.OpenAsync(owner, TestContext.Current.CancellationToken);
        using var client = factory.CreateClient(); using var response = await ReadAsync(client, session);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("Introduction", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<PlaybackTestContext> SeedAsync()
    {
        var test = new PlaybackTestContext(factory); await test.SeedAsync(true, TestContext.Current.CancellationToken); return test;
    }

    private async Task CurrentAsync(PlaybackTestContext test)
    {
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var attempt = Transcription.Create(new(test.Tenant, test.VideoId, "requested", test.Student, DateTimeOffset.UtcNow));
        db.Transcriptions.Add(attempt); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_access.video_transcriptions SET status = 'working' WHERE transcription_id = {attempt.TranscriptionId}", TestContext.Current.CancellationToken);
        await db.Entry(attempt).ReloadAsync(TestContext.Current.CancellationToken);
        var content = TranscriptionContent.Create(new(attempt.TranscriptionId, test.Tenant, "pt-BR", 60_000, [new(0, 1000, "Introduction & <script>"), new(1000, 2000, "Second caption")]));
        attempt.MarkReady(content, DateTimeOffset.UtcNow); db.TranscriptionContents.Add(content);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> ReadAsync(HttpClient client, Guid session)
        => client.GetAsync($"/internal/v1/playback-sessions/{session:D}/captions", TestContext.Current.CancellationToken);
    private static async Task AssertProblemAsync(HttpClient client, Guid session, int status, string code)
    {
        using var response = await ReadAsync(client, session); Assert.Equal(status, (int)response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, body.GetProperty("code").GetString()); Assert.False(body.TryGetProperty("segments", out _));
    }
}
