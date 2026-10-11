using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
public sealed class VideoUploadTagTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(CreateNormalizesDeduplicatesAndUsesExistingCanonicalSpelling))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task CreateNormalizesDeduplicatesAndUsesExistingCanonicalSpelling()
    {
        var actor = Actor();
        var video = await SeedAsync(actor.TenantId);
        using var replaced = await ReplaceAsync(video.VideoId, actor, ["Docker"]);
        var storage = new UploadStorage();
        using var app = App(storage);
        using var client = app.CreateClient();
        using var response = await StartAsync(client, actor, [" docker ", "DOCKER", "  Módulo   2  "]);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "Docker", "Módulo 2" }, await TagsAsync(response));
        var uploadId = await IdAsync(response, "uploadId");
        using var pending = await SendAsync(client, actor, HttpMethod.Get, $"/internal/v1/video-uploads/{uploadId}");
        Assert.Equal(new[] { "Docker", "Módulo 2" }, await TagsAsync(pending));
        Assert.Equal(1, storage.Initiated);
    }

    [Fact(DisplayName = nameof(OmittedTagsCreateAndCompleteWithEmptySet))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task OmittedTagsCreateAndCompleteWithEmptySet()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var created = await StartAsync(client, actor, null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Empty(await TagsAsync(created));
        using var complete = await CompleteAsync(client, actor, await IdAsync(created, "uploadId"));
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
        Assert.Empty(await TagsAsync(complete));
    }

    [Theory(DisplayName = nameof(InvalidTagsDoNotStartStorageOrPersistUpload))]
    [InlineData(" ")]
    [InlineData("bad\nvalue")]
    [InlineData("abcdefghijklmnopqrstuvwxyz12345")]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task InvalidTagsDoNotStartStorageOrPersistUpload(string tag)
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var response = await StartAsync(client, actor, [tag]);
        await ProblemAsync(response, "TAG_INVALID");
        Assert.Equal(0, storage.Initiated); Assert.Equal(0, storage.Signed);
        await using var scope = Scope(actor.TenantId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<MediaDbContext>().VideoUploads.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(ElevenDistinctTagsDoNotStartStorage))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task ElevenDistinctTagsDoNotStartStorage()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var response = await StartAsync(client, actor, Enumerable.Range(1, 11).Select(index => $"tag{index}").ToArray());
        await ProblemAsync(response, "TAGS_LIMIT_EXCEEDED");
        Assert.Equal(0, storage.Initiated); Assert.Equal(0, storage.Signed);
    }

    [Fact(DisplayName = nameof(IdempotencyKeyIncludesTags))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task IdempotencyKeyIncludesTags()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var first = await StartAsync(client, actor, ["Original"], "same-key");
        using var replay = await StartAsync(client, actor, ["Original"], "same-key");
        using var conflict = await StartAsync(client, actor, ["Other"], "same-key");
        Assert.Equal(await IdAsync(first, "uploadId"), await IdAsync(replay, "uploadId"));
        await ProblemAsync(conflict, "IDEMPOTENCY_KEY_REUSED");
        Assert.Equal(1, storage.Initiated);
    }

    [Fact(DisplayName = nameof(ResumptionKeepsPendingTagsAndUpload))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task ResumptionKeepsPendingTagsAndUpload()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var first = await StartAsync(client, actor, ["Original"]);
        using var resumed = await StartAsync(client, actor, ["Other"]);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(await IdAsync(first, "uploadId"), await IdAsync(resumed, "uploadId"));
        Assert.Equal(new[] { "Original" }, await TagsAsync(resumed));
        Assert.Equal(1, storage.Initiated);
    }

    [Fact(DisplayName = nameof(CompletionUsesCanonicalSpellingCreatedDuringUploadAndListsReceivedVideo))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task CompletionUsesCanonicalSpellingCreatedDuringUploadAndListsReceivedVideo()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var started = await StartAsync(client, actor, ["docker", "Módulo 2"]);
        var otherVideo = await SeedAsync(actor.TenantId);
        using var replaced = await ReplaceAsync(otherVideo.VideoId, actor, ["Docker"]);
        using var completed = await CompleteAsync(client, actor, await IdAsync(started, "uploadId"));
        Assert.Equal(HttpStatusCode.Created, completed.StatusCode);
        Assert.Equal(new[] { "Docker", "Módulo 2" }, await TagsAsync(completed));
        using var json = await JsonAsync(completed);
        Assert.Equal("received", json.RootElement.GetProperty("status").GetString());
        var videoId = json.RootElement.GetProperty("videoId").GetGuid();
        using var list = await SendAsync(client, actor, HttpMethod.Get, "/internal/v1/videos");
        using var page = await JsonAsync(list);
        var listed = page.RootElement.GetProperty("data").EnumerateArray().Single(video => video.GetProperty("videoId").GetGuid() == videoId);
        Assert.Equal(new[] { "Docker", "Módulo 2" }, ReadTags(listed));
        Assert.Equal(1, storage.Completed);
        await using var scope = Scope(actor.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(2, await db.VideoTags.CountAsync(link => link.VideoId == videoId, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(RepeatedCompletionReturnsCurrentTagsWithoutCreatingAnotherVideo))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task RepeatedCompletionReturnsCurrentTagsWithoutCreatingAnotherVideo()
    {
        var actor = Actor(); var storage = new UploadStorage();
        using var app = App(storage); using var client = app.CreateClient();
        using var started = await StartAsync(client, actor, ["Original"]);
        var uploadId = await IdAsync(started, "uploadId");
        using var first = await CompleteAsync(client, actor, uploadId, "completion-key");
        var videoId = await IdAsync(first, "videoId");
        using var unchanged = await CompleteAsync(client, actor, uploadId, "another-key");
        Assert.Equal(new[] { "Original" }, await TagsAsync(unchanged));
        using var replaced = await ReplaceAsync(videoId, actor, ["Current"]);
        using var replay = await CompleteAsync(client, actor, uploadId, "completion-key");
        using var again = await CompleteAsync(client, actor, uploadId);
        foreach (var response in new[] { replay, again })
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(videoId, await IdAsync(response, "videoId"));
            Assert.Equal(new[] { "Current" }, await TagsAsync(response));
        }
        Assert.Equal(1, storage.Completed);
        await using var scope = Scope(actor.TenantId);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Videos.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(CompletionCountsAssignmentsOnceByOrigin))]
    [Trait("Layer", "Video upload tags - Integration")]
    public async Task CompletionCountsAssignmentsOnceByOrigin()
    {
        var actor = Actor(); var storage = new UploadStorage();
        var other = await SeedAsync(actor.TenantId);
        using var assigned = await ReplaceAsync(other.VideoId, actor, ["Existing"]);
        var origins = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "media.video_tags.assigned") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "origin")
                    for (var index = 0; index < measurement; index++) origins.Add((string)tag.Value!);
        });
        listener.Start();
        using var app = App(storage); using var client = app.CreateClient();
        using var started = await StartAsync(client, actor, ["Existing", "New"]);
        Assert.Empty(origins);
        var uploadId = await IdAsync(started, "uploadId");
        using var first = await CompleteAsync(client, actor, uploadId, "metric-key");
        using var replay = await CompleteAsync(client, actor, uploadId, "metric-key");
        using var again = await CompleteAsync(client, actor, uploadId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(new[] { "existing", "new" }, origins.Order(StringComparer.Ordinal));
    }

    private UploadActor Actor()
    {
        var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7();
        return new UploadActor(tenant, factory.CreateTokenForActor(tenant, actor, "midia.enviar"));
    }

    private WebApplicationFactory<Program> App(UploadStorage storage) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IMediaStoragePort>(storage))));

    private async Task<Video> SeedAsync(Guid tenant)
    {
        var video = Video.Create(tenant, "Another video", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        await using var scope = Scope(tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        db.Videos.Add(video); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return video;
    }

    private AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        return scope;
    }

    private async Task<HttpResponseMessage> ReplaceAsync(Guid videoId, UploadActor actor, string[] tags)
    {
        using var client = factory.CreateClient();
        return await SendAsync(client, actor, HttpMethod.Put, $"/internal/v1/videos/{videoId}/tags", new { tags }, Guid.CreateVersion7().ToString());
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, UploadActor actor, string[]? tags, string? key = null)
    {
        var body = new Dictionary<string, object>
        {
            ["title"] = "Tagged video",
            ["fileName"] = "aula.mp4",
            ["fileSize"] = 17,
            ["contentType"] = "video/mp4",
            ["fingerprint"] = "upload-tags-fingerprint",
            ["uploaderName"] = "Teacher",
        };
        if (tags is not null) body["tags"] = tags;
        return SendAsync(client, actor, HttpMethod.Post, "/internal/v1/video-uploads", body, key ?? Guid.CreateVersion7().ToString());
    }

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, UploadActor actor, Guid uploadId, string? key = null)
        => SendAsync(client, actor, HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId}/complete", key: key ?? Guid.CreateVersion7().ToString());

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, UploadActor actor, HttpMethod method, string path, object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", actor.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    private static async Task<Guid> IdAsync(HttpResponseMessage response, string property)
    {
        using var json = await JsonAsync(response); return json.RootElement.GetProperty(property).GetGuid();
    }
    private static string[] ReadTags(JsonElement root) => root.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray();
    private static async Task<string[]> TagsAsync(HttpResponseMessage response)
    {
        using var json = await JsonAsync(response); return ReadTags(json.RootElement);
    }
    private static async Task ProblemAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = await JsonAsync(response); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private sealed record UploadActor(Guid TenantId, string Token);

    private sealed class UploadStorage : IMediaStoragePort
    {
        public int Initiated { get; private set; }
        public int Signed { get; private set; }
        public int Completed { get; private set; }
        public Task<string> InitiateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
        { Initiated++; return Task.FromResult("storage-upload"); }
        public Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MediaUploadPart>>([new(1, "part-etag", 17)]);
        public Task<Uri> CreatePartUploadUriAsync(string objectKey, string storageUploadId, int partNumber, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        { Signed++; return Task.FromResult(new Uri("http://storage.test/part")); }
        public Task CompleteMultipartUploadAsync(string objectKey, string storageUploadId, IReadOnlyList<MediaUploadPart> parts, CancellationToken cancellationToken)
        { Completed++; return Task.CompletedTask; }
        public Task AbortMultipartUploadAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UploadDirectoryAsync(string sourceDirectory, string objectPrefix, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
