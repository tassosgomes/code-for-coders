using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoTagReplacementTests(VideoLibraryApiFactory factory)
{
    [Fact]
    public async Task ReplacesAndClearsWithoutChangingVideoOrOutbox()
    {
        var video = await SeedAsync();
        using var first = await ReplaceAsync(video, ["Docker", "Módulo 2"]);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(new[] { "Docker", "Módulo 2" }, await TagsAsync(first));
        using var cleared = await ReplaceAsync(video, []);
        Assert.Empty(await TagsAsync(cleared));
        await using var scope = Scope(video.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var current = await db.Videos.SingleAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken);
        Assert.Equal(video.Title, current.Title);
        Assert.Equal(video.Status, current.Status);
        Assert.Equal(video.UploadedByAccountId, current.UploadedByAccountId);
        Assert.Empty(await db.OutboxMessages.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.Tags.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UsesCanonicalSchoolSpellingAndDeduplicates()
    {
        var first = await SeedAsync();
        using var created = await ReplaceAsync(first, ["Docker"]);
        var second = await SeedAsync(first.TenantId);
        using var response = await ReplaceAsync(second, ["docker", " Docker ", "DOCKER"]);
        Assert.Equal(new[] { "Docker" }, await TagsAsync(response));
    }

    [Theory]
    [InlineData(" ", "TAG_INVALID")]
    [InlineData("abcdefghijklmnopqrstuvwxzy12345", "TAG_INVALID")]
    [InlineData("bad\nvalue", "TAG_INVALID")]
    public async Task InvalidTagLeavesPreviousSetUnchanged(string tag, string code)
    {
        var video = await SeedAsync();
        using var initial = await ReplaceAsync(video, ["Original"]);
        using var response = await ReplaceAsync(video, [tag]);
        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, code);
        Assert.Equal(new[] { "Original" }, await CurrentTagsAsync(video));
    }

    [Fact]
    public async Task ElevenDistinctTagsAreRejectedAtomically()
    {
        var video = await SeedAsync();
        using var response = await ReplaceAsync(video, Enumerable.Range(1, 11).Select(index => $"tag{index}").ToArray());
        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "TAGS_LIMIT_EXCEEDED");
        Assert.Empty(await CurrentTagsAsync(video));
    }

    [Theory]
    [InlineData(51, 1)]
    [InlineData(1, 101)]
    public async Task RawProtectionLimitsReturnBadRequest(int count, int length)
    {
        var video = await SeedAsync();
        using var response = await ReplaceAsync(video, Enumerable.Repeat(new string('a', length), count).ToArray());
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
    }

    [Fact]
    public async Task AnotherSchoolCannotReplaceOrReadTags()
    {
        var video = await SeedAsync();
        using var initial = await ReplaceAsync(video, ["Secret"]);
        using var denied = await ReplaceAsync(video, ["Foreign"], tenant: Guid.CreateVersion7());
        await AssertProblemAsync(denied, HttpStatusCode.NotFound, "VIDEO_NOT_FOUND");
        Assert.Equal(new[] { "Secret" }, await CurrentTagsAsync(video));
    }

    [Fact]
    public async Task ReplayReturnsSameResponseAndDifferentBodyConflicts()
    {
        var video = await SeedAsync();
        var actor = Guid.CreateVersion7();
        var key = Guid.CreateVersion7().ToString();
        using var first = await ReplaceAsync(video, ["Docker"], key, actor);
        using var replay = await ReplaceAsync(video, ["Docker"], key, actor);
        using var conflict = await ReplaceAsync(video, ["Other"], key, actor);
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await AssertProblemAsync(conflict, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task ConcurrentReplacementsLeaveOneWholeSet()
    {
        var video = await SeedAsync();
        string[] a = ["A1", "A2"]; string[] b = ["B1", "B2"];
        var responses = await Task.WhenAll(ReplaceAsync(video, a), ReplaceAsync(video, b));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        var current = await CurrentTagsAsync(video);
        Assert.True(current.SequenceEqual(a) || current.SequenceEqual(b));
    }

    [Fact]
    public async Task ConcurrentReplayWithSameKeyCommitsOnce()
    {
        var video = await SeedAsync(); var actor = Guid.CreateVersion7(); var key = Guid.CreateVersion7().ToString();
        var responses = await Task.WhenAll(ReplaceAsync(video, ["Docker"], key, actor), ReplaceAsync(video, ["Docker"], key, actor));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        await using var scope = Scope(video.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(1, await db.OperationIdempotencyRecords.CountAsync(record => record.Key == key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplacementDoesNotInvalidatePreparationLease()
    {
        var video = await SeedAsync(); var lease = Guid.CreateVersion7();
        await using var scope = Scope(video.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var preparing = await db.Videos.SingleAsync(row => row.VideoId == video.VideoId, TestContext.Current.CancellationToken);
        preparing.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var response = await ReplaceAsync(video, ["Docker"]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        preparing.MarkReady(lease, 10, 1024, new byte[32], "test-key");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Docker" }, await CurrentTagsAsync(video));
    }

    [Fact]
    public async Task ListGetAndRenameIncludeCanonicalTags()
    {
        var video = await SeedAsync();
        using var assigned = await ReplaceAsync(video, ["Zebra", "Álgebra"]);
        using var client = factory.CreateClient();
        foreach (var path in new[] { $"/internal/v1/videos/{video.VideoId}", "/internal/v1/videos" })
        {
            using var response = await client.SendAsync(Request(HttpMethod.Get, path, video.TenantId), TestContext.Current.CancellationToken);
            using var json = await ReadAsync(response);
            var root = path.EndsWith("/videos", StringComparison.Ordinal) ? json.RootElement.GetProperty("data")[0] : json.RootElement;
            Assert.Equal(new[] { "Álgebra", "Zebra" }, ReadTags(root));
        }
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId}", video.TenantId);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString()); request.Content = JsonContent.Create(new { title = "Renamed" });
        using var renamed = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Álgebra", "Zebra" }, await TagsAsync(renamed));
    }

    [Fact]
    public async Task LegacyRenameReplayIncludesEmptyTags()
    {
        var video = await SeedAsync(); var actor = Guid.CreateVersion7(); var key = Guid.CreateVersion7().ToString();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { VideoId = video.VideoId, Title = "Legacy" })));
        var record = OperationIdempotencyRecord.Create(video.TenantId, actor, "updateVideoTitle", key, hash, DateTimeOffset.UtcNow.AddHours(24));
        record.SetResponse(200, JsonSerializer.Serialize(new
        {
            video.VideoId,
            Title = "Legacy",
            video.Status,
            UploadedBy = new { AccountId = video.UploadedByAccountId, Name = video.UploadedByName },
            video.UploadedAt,
            video.DurationSeconds,
            video.FailureReason
        }), DateTimeOffset.UtcNow.AddHours(24));
        await using (var scope = Scope(video.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>(); db.OperationIdempotencyRecords.Add(record);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId}", video.TenantId, actor);
        request.Headers.Add("Idempotency-Key", key); request.Content = JsonContent.Create(new { title = "Legacy" });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Empty(await TagsAsync(response));
    }

    private async Task<Video> SeedAsync(Guid? tenant = null)
    {
        var video = Video.Create(tenant ?? Guid.CreateVersion7(), "Original", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        await using var scope = Scope(video.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>(); db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return video;
    }
    private Microsoft.Extensions.DependencyInjection.AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant); return scope;
    }
    private async Task<HttpResponseMessage> ReplaceAsync(Video video, string[] tags, string? key = null, Guid? actor = null, Guid? tenant = null)
    {
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Put, $"/internal/v1/videos/{video.VideoId}/tags", tenant ?? video.TenantId, actor);
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); request.Content = JsonContent.Create(new { tags });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private HttpRequestMessage Request(HttpMethod method, string path, Guid tenant, Guid? actor = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(tenant, actor ?? Guid.CreateVersion7(), "midia.enviar")); return request;
    }
    private async Task<string[]> CurrentTagsAsync(Video video)
    {
        using var client = factory.CreateClient(); using var response = await client.SendAsync(Request(HttpMethod.Get, $"/internal/v1/videos/{video.VideoId}", video.TenantId), TestContext.Current.CancellationToken);
        return await TagsAsync(response);
    }
    private static string[] ReadTags(JsonElement root) => root.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray();
    private static async Task<string[]> TagsAsync(HttpResponseMessage response) { using var json = await ReadAsync(response); return ReadTags(json.RootElement); }
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); using var json = await ReadAsync(response); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
}
