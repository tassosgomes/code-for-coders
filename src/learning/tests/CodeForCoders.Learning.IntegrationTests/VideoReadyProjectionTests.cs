using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.VideoProjection;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(VideoProjectionApiCollection.Name)]
public sealed class VideoReadyProjectionTests(VideoProjectionApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string Path(Guid course) => $"/internal/v1/courses/{course}";
    private static JsonElement Lesson(JsonElement course) => course.GetProperty("modules")[0].GetProperty("lessons")[0];
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());

    [Fact(DisplayName = nameof(ReadyFactAllowsSchoolColleagueVideoSwapAndUnlinkWithoutChangingLessonIdentity))]
    public async Task ReadyFactAllowsSchoolColleagueVideoSwapAndUnlinkWithoutChangingLessonIdentity()
    {
        var tenant = Guid.CreateVersion7();
        using var client = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var (course, _, lesson) = await DraftAsync(client);
        var first = Guid.CreateVersion7(); var second = Guid.CreateVersion7();
        await PublishReadyAsync(tenant, first); await PublishReadyAsync(tenant, second);
        using var colleague = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var linked = await WriteAsync(colleague, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = first });
        Assert.Equal(first, Lesson(linked).GetProperty("video").GetProperty("videoId").GetGuid());
        var swapped = await WriteAsync(client, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = second });
        Assert.Equal(second, Lesson(swapped).GetProperty("video").GetProperty("videoId").GetGuid());
        Assert.Single(Lesson(swapped).GetProperty("video").EnumerateObject());
        var unlinked = await WriteAsync(client, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = (Guid?)null });
        Assert.Equal(lesson, Lesson(unlinked).GetProperty("lessonId").GetGuid());
        Assert.Equal(JsonValueKind.Null, Lesson(unlinked).GetProperty("video").ValueKind);
        var loaded = await client.GetFromJsonAsync<JsonElement>(Path(course), Cancellation);
        Assert.Equal(JsonValueKind.Null, Lesson(loaded).GetProperty("video").ValueKind);
    }

    [Theory(DisplayName = nameof(UnavailableVideoIsRejectedWithoutChangingLinkOrDraftRevision))]
    [InlineData("received")]
    [InlineData("preparing")]
    [InlineData("failed")]
    [InlineData("missing")]
    [InlineData("other-tenant")]
    public async Task UnavailableVideoIsRejectedWithoutChangingLinkOrDraftRevision(string status)
    {
        var tenant = Guid.CreateVersion7();
        using var client = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var (course, _, lesson) = await DraftAsync(client);
        var valid = Guid.CreateVersion7(); await PublishReadyAsync(tenant, valid);
        var before = await WriteAsync(client, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = valid });
        var invalid = Guid.CreateVersion7();
        if (status == "other-tenant") await PublishReadyAsync(Guid.CreateVersion7(), invalid);
        if (status == "failed") await PublishFactAsync(new(Guid.CreateVersion7(), tenant, invalid, DateTimeOffset.UtcNow, false));
        using var request = Request(HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = invalid, title = "Must not change" });
        using var rejected = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("VIDEO_NOT_AVAILABLE", problem.GetProperty("code").GetString());
        var after = await client.GetFromJsonAsync<JsonElement>(Path(course), Cancellation);
        Assert.Equal(before.GetProperty("draftRevision").GetInt32(), after.GetProperty("draftRevision").GetInt32());
        Assert.Equal(valid, Lesson(after).GetProperty("video").GetProperty("videoId").GetGuid());
        Assert.Equal("Lesson", Lesson(after).GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(NewlyReadyFactMakesPreviouslyUnknownVideoBindableOnRetry))]
    public async Task NewlyReadyFactMakesPreviouslyUnknownVideoBindableOnRetry()
    {
        var tenant = Guid.CreateVersion7(); var video = Guid.CreateVersion7();
        using var client = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var (course, _, lesson) = await DraftAsync(client); var key = Guid.CreateVersion7().ToString();
        using var first = Request(HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = video }, key);
        using var failed = await client.SendAsync(first, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        await PublishReadyAsync(tenant, video);
        using var retry = Request(HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = video }, key);
        using var successful = await client.SendAsync(retry, Cancellation);
        Assert.Equal(HttpStatusCode.OK, successful.StatusCode);
    }

    [Fact(DisplayName = nameof(RedeliveryAndHistoricalReplayAreDurablyDeduplicatedByOriginalEventId))]
    public async Task RedeliveryAndHistoricalReplayAreDurablyDeduplicatedByOriginalEventId()
    {
        _ = factory.CreateClient();
        var fact = new VideoAvailabilityFact(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddDays(-3), true);
        await PublishFactAsync(fact); await PublishFactAsync(fact);
        await using var context = Context();
        Assert.Equal(1, await context.VideoFactReceipts.IgnoreQueryFilters().CountAsync(receipt => receipt.EventId == fact.EventId, Cancellation));
        Assert.Equal(1, await context.ProjectedVideos.IgnoreQueryFilters().CountAsync(video => video.TenantId == fact.TenantId && video.VideoId == fact.VideoId, Cancellation));
        using var client = factory.Actor(fact.TenantId, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var (course, _, lesson) = await DraftAsync(client);
        await WriteAsync(client, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { videoId = fact.VideoId });
    }

    [Fact(DisplayName = nameof(OlderFactCannotRegressNewerProjection))]
    public async Task OlderFactCannotRegressNewerProjection()
    {
        _ = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        var fact = new VideoAvailabilityFact(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, true);
        await PublishFactAsync(fact);
        await PublishFactAsync(fact with { EventId = Guid.CreateVersion7(), OccurredAt = now.AddDays(-1), IsReady = false });
        await using var context = Context();
        Assert.True(await context.ProjectedVideos.IgnoreQueryFilters().Where(video => video.VideoId == fact.VideoId).Select(video => video.IsReady).SingleAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ReconciliationIsTenantScopedAndBlocksMissingOrExtraReadyIds))]
    public async Task ReconciliationIsTenantScopedAndBlocksMissingOrExtraReadyIds()
    {
        _ = factory.CreateClient();
        var tenant = Guid.CreateVersion7(); var video = Guid.CreateVersion7();
        await PublishReadyAsync(tenant, video); await PublishReadyAsync(Guid.CreateVersion7(), Guid.CreateVersion7());
        await using var context = Context(); var reconciler = new VideoProjectionReconciler(context);
        Assert.True(await reconciler.MatchesAsync(tenant, new HashSet<Guid> { video }, Cancellation));
        Assert.False(await reconciler.MatchesAsync(tenant, new HashSet<Guid> { video, Guid.CreateVersion7() }, Cancellation));
        Assert.False(await reconciler.MatchesAsync(tenant, new HashSet<Guid>(), Cancellation));
    }

    [Fact(DisplayName = nameof(MalformedFactGoesToDurableDeadLetterQueueAndDoesNotCreateProjection))]
    public async Task MalformedFactGoesToDurableDeadLetterQueueAndDoesNotCreateProjection()
    {
        _ = factory.CreateClient();
        var connection = factory.Services.GetRequiredService<RabbitMqConnectionProvider>();
        await using var channel = await connection.CreatePublisherChannelAsync(Cancellation);
        await channel.BasicPublishAsync("media.events", VideoAvailabilityFact.ReadyRoute, true,
            new BasicProperties { MessageId = Guid.CreateVersion7().ToString(), Persistent = true }, "{}"u8.ToArray(), Cancellation);
        await EventuallyAsync(async () => (await channel.QueueDeclarePassiveAsync("learning.video-availability.dlq", Cancellation)).MessageCount > 0);
    }

    [Fact(DisplayName = nameof(CreationWithReadyVideoUsesTheSameTenantValidation))]
    public async Task CreationWithReadyVideoUsesTheSameTenantValidation()
    {
        var tenant = Guid.CreateVersion7();
        using var client = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
        var (course, module, _) = await DraftAsync(client); var video = Guid.CreateVersion7();
        await PublishReadyAsync(tenant, video);
        var created = await WriteAsync(client, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Second lesson", videoId = video });
        Assert.Equal(video, created.GetProperty("modules")[0].GetProperty("lessons")[1].GetProperty("video").GetProperty("videoId").GetGuid());
    }

    private Task PublishReadyAsync(Guid tenant, Guid video) => PublishFactAsync(new(Guid.CreateVersion7(), tenant, video, DateTimeOffset.UtcNow, true));

    private async Task PublishFactAsync(VideoAvailabilityFact fact)
    {
        var connection = factory.Services.GetRequiredService<RabbitMqConnectionProvider>();
        await using var channel = await connection.CreatePublisherChannelAsync(Cancellation);
        var route = fact.IsReady ? VideoAvailabilityFact.ReadyRoute : VideoAvailabilityFact.FailedRoute;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { fact.EventId, fact.TenantId, fact.VideoId, fact.OccurredAt, durationSeconds = 60, reason = "invalid-video" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await channel.BasicPublishAsync("media.events", route, true, new BasicProperties { MessageId = fact.EventId.ToString(), Persistent = true }, payload, Cancellation);
        await EventuallyAsync(async () => { await using var context = Context(); return await context.VideoFactReceipts.IgnoreQueryFilters().AnyAsync(receipt => receipt.EventId == fact.EventId, Cancellation); });
        await EventuallyAsync(async () => (await channel.QueueDeclarePassiveAsync("learning.video-availability", Cancellation)).MessageCount == 0);
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (!await condition()) await Task.Delay(50, timeout.Token);
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, object body, string? key = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); return request;
    }

    private static async Task<JsonElement> WriteAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        using var request = Request(method, path, body); using var response = await client.SendAsync(request, Cancellation);
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }

    private static async Task<(Guid Course, Guid Module, Guid Lesson)> DraftAsync(HttpClient client)
    {
        var course = (await WriteAsync(client, HttpMethod.Post, "/internal/v1/courses", new { title = "Course" })).GetProperty("courseId").GetGuid();
        var module = (await WriteAsync(client, HttpMethod.Post, Path(course) + "/modules", new { title = "Module" })).GetProperty("modules")[0].GetProperty("moduleId").GetGuid();
        var lesson = Lesson(await WriteAsync(client, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Lesson" })).GetProperty("lessonId").GetGuid();
        return (course, module, lesson);
    }
}
