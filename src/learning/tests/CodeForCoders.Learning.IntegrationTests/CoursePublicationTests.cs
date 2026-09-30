using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Messaging;
using CodeForCoders.Learning.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(CourseApiCollection.Name)]
public sealed class CoursePublicationTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);

    [Fact(DisplayName = nameof(NoModulesReturnsPendencyAndNoPublication))]
    public async Task NoModulesReturnsPendencyAndNoPublication()
    {
        var course = await SeedAsync(false); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("course-without-modules", problem.GetProperty("pendencies")[0].GetProperty("code").GetString());
        await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(AllMissingLessonsAndVideosAreReportedInPedagogicalOrder))]
    public async Task AllMissingLessonsAndVideosAreReportedInPedagogicalOrder()
    {
        var course = await SeedAsync(false, incomplete: true); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var pendencies = problem.GetProperty("pendencies"); Assert.Equal(3, pendencies.GetArrayLength());
        Assert.Equal("module-without-lessons", pendencies[0].GetProperty("code").GetString());
        Assert.Equal(course.Modules[1].Lessons[0].Id, pendencies[1].GetProperty("lessonId").GetGuid());
        Assert.Equal(course.Modules[1].Lessons[1].Id, pendencies[2].GetProperty("lessonId").GetGuid());
        await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(CompleteCourseCommitsImmutableVersionFactAndCorrelatedAct))]
    public async Task CompleteCourseCommitsImmutableVersionFactAndCorrelatedAct()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course, note: "Private version note");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/versions/1", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        var version = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(1, version.GetProperty("versionNumber").GetInt32());
        Assert.Equal("Teacher display name", version.GetProperty("publishedBy").GetProperty("name").GetString());
        await AssertCountsAsync(course, 1, 2);
        await using var context = Context();
        var rows = await context.ContentOutboxMessages.IgnoreQueryFilters().Where(row => row.TenantId == course.TenantId).ToListAsync(Cancellation);
        var fact = rows.Single(row => row.RoutingKey == "conteudo.versao-publicada.v1");
        var act = rows.Single(row => row.RoutingKey == "auditoria.ato-praticado.v1");
        Assert.NotEqual(fact.Id, act.Id);
        using var payload = JsonDocument.Parse(act.Payload);
        Assert.Equal(fact.Id, payload.RootElement.GetProperty("fatoId").GetGuid());
        Assert.Equal("curso", payload.RootElement.GetProperty("alvo").GetProperty("tipo").GetString());
        Assert.Equal("conteudo", payload.RootElement.GetProperty("origem").GetString());
        Assert.False(payload.RootElement.TryGetProperty("motivo", out _));
        foreach (var row in rows)
        {
            Assert.DoesNotContain("Teacher display name", row.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain("Private version note", row.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain("Private description", row.Payload, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = nameof(StaleRevisionReturnsConflictAndDoesNotPublish))]
    public async Task StaleRevisionReturnsConflictAndDoesNotPublish()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course, revision: course.DraftRevision + 1);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DRAFT_CHANGED", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(NoteOverLimitIsRejectedAtomically))]
    public async Task NoteOverLimitIsRejectedAtomically()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course, note: new string('x', 1001));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(ReaderCannotPublish))]
    public async Task ReaderCannotPublish()
    {
        var course = await SeedAsync(); using var client = factory.Actor(course.TenantId, Guid.CreateVersion7(), ["autoria.ler"]);
        using var response = await PublishAsync(client, course); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(OtherTenantCourseIsIndistinguishableFromMissing))]
    public async Task OtherTenantCourseIsIndistinguishableFromMissing()
    {
        var course = await SeedAsync(); using var client = Teacher(Guid.CreateVersion7());
        using var response = await PublishAsync(client, course); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertCountsAsync(course, 0, 0);
    }

    [Fact(DisplayName = nameof(RetryWithSameKeyReturnsSameStatusBodyAndLocation))]
    public async Task RetryWithSameKeyReturnsSameStatusBodyAndLocation()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var key = Guid.CreateVersion7().ToString();
        using var first = await PublishAsync(client, course, key); using var retry = await PublishAsync(client, course, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode); Assert.Equal(first.Headers.Location, retry.Headers.Location);
        Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await retry.Content.ReadAsStringAsync(Cancellation));
        await AssertCountsAsync(course, 1, 2);
    }

    [Fact(DisplayName = nameof(SameKeyWithChangedBodyIsRejected))]
    public async Task SameKeyWithChangedBodyIsRejected()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var key = Guid.CreateVersion7().ToString();
        using var first = await PublishAsync(client, course, key, note: "First");
        using var changed = await PublishAsync(client, course, key, note: "Changed");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        await AssertCountsAsync(course, 1, 2);
    }

    [Fact(DisplayName = nameof(FailureBetweenOutboxRowsRollsBackVersionAndFactAndAllowsRetry))]
    public async Task FailureBetweenOutboxRowsRollsBackVersionAndFactAndAllowsRetry()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var key = Guid.CreateVersion7().ToString();
        factory.FailPublicationOutbox = true;
        try
        {
            using var failed = await PublishAsync(client, course, key); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            await AssertCountsAsync(course, 0, 0);
        }
        finally { factory.FailPublicationOutbox = false; }
        using var retry = await PublishAsync(client, course, key); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await AssertCountsAsync(course, 1, 2);
    }

    [Fact(DisplayName = nameof(DraftEditsDoNotChangePublishedSnapshot))]
    public async Task DraftEditsDoNotChangePublishedSnapshot()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var published = await PublishAsync(client, course); Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/internal/v1/courses/{course.Id}") { Content = JsonContent.Create(new { title = "Changed" }) };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var edited = await client.SendAsync(request, Cancellation); Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        await using var context = Context();
        var version = await context.CourseVersions.IgnoreQueryFilters().SingleAsync(item => item.CourseId == course.Id, Cancellation);
        Assert.Equal(course.Title, version.Title); Assert.Equal(course.Modules[0].Lessons[0].Id, version.Modules[0].Lessons[0].LessonId);
        Assert.True(await context.Courses.IgnoreQueryFilters().Where(item => item.Id == course.Id).Select(item => item.HasUnpublishedChanges).SingleAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentSameIntentCreatesExactlyOneVersion))]
    public async Task ConcurrentSameIntentCreatesExactlyOneVersion()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var key = Guid.CreateVersion7().ToString();
        var responses = await Task.WhenAll(PublishAsync(client, course, key), PublishAsync(client, course, key));
        foreach (var response in responses) { using (response) Assert.Equal(HttpStatusCode.Created, response.StatusCode); }
        await AssertCountsAsync(course, 1, 2);
    }

    [Fact(DisplayName = nameof(PublisherRoutesRealOutboxRowsAndMaximumCurriculumThroughBroker))]
    public async Task PublisherRoutesRealOutboxRowsAndMaximumCurriculumThroughBroker()
    {
        await using var rabbit = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
            .WithUsername("publication").WithPassword("publication-tests")
            .WithEnvironment("RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS", "-rabbit max_message_size 67108864").Build();
        await rabbit.StartAsync(Cancellation);
        var options = Options.Create(new RabbitMqOptions { Host = rabbit.Hostname, Port = rabbit.GetMappedPublicPort(5672), Username = "publication", Password = "publication-tests" });
        await using var provider = new RabbitMqConnectionProvider(options);
        await new RabbitMqTopologyInitializer(provider, options).StartAsync(Cancellation);
        await using var channel = await provider.CreateChannelAsync(Cancellation);
        foreach (var binding in new[] { ("publication-facts", "learning.events", "conteudo.versao-publicada.v1"), ("publication-acts", "audit.events", "auditoria.ato-praticado.v1") })
        {
            await channel.QueueDeclareAsync(binding.Item1, true, false, false, new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Cancellation);
            await channel.QueueBindAsync(binding.Item1, binding.Item2, binding.Item3, cancellationToken: Cancellation);
        }
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var response = await PublishAsync(client, course); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var context = Context();
        var rows = await context.ContentOutboxMessages.IgnoreQueryFilters().Where(row => row.TenantId == course.TenantId).ToListAsync(Cancellation);
        var publisher = new RabbitMqPublisher(provider, options);
        foreach (var row in rows) await publisher.PublishAsync(row, Cancellation);
        foreach (var queue in new[] { "publication-facts", "publication-acts" })
        {
            var delivery = await channel.BasicGetAsync(queue, true, Cancellation); Assert.NotNull(delivery);
            Assert.Equal(queue == "publication-facts" ? "learning.events" : "audit.events", delivery.Exchange);
        }
        var maximumTitle = new string('\uffff', 200);
        var payload = new
        {
            EventId = Guid.CreateVersion7(),
            course.TenantId,
            CourseId = course.Id,
            VersionNumber = 1,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishedById = Guid.CreateVersion7(),
            Title = maximumTitle,
            Modules = Enumerable.Range(1, 100).Select(position => new
            {
                ModuleId = Guid.CreateVersion7(),
                Title = maximumTitle,
                Position = position,
                Lessons = Enumerable.Range(1, 200).Select(index => new { LessonId = Guid.CreateVersion7(), Title = maximumTitle, Position = index, VideoId = Guid.CreateVersion7() })
            })
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var size = System.Text.Encoding.UTF8.GetByteCount(json);
        Assert.InRange(size, 16777217, 67108864);
        const string route = "conteudo.versao-publicada.v1";
        var draft = new CodeForCoders.Learning.Application.Interfaces.OutboxMessageDraft(payload.EventId, course.TenantId, route, route, payload, payload.PublishedAt, null);
        await publisher.PublishAsync(CodeForCoders.Learning.Infra.Data.Outbox.OutboxMessage.Create(draft, json), Cancellation);
        var maximum = await channel.BasicGetAsync("publication-facts", true, Cancellation); Assert.NotNull(maximum); Assert.Equal(size, maximum.Body.Length);
        await File.WriteAllTextAsync("/tmp/run.TDLCPE8Y-max-fact.json", JsonSerializer.Serialize(new { bytes = size, broker_limit_bytes = 67108864, accepted = true }), Cancellation);
    }

    private static CourseChanges Changes(string title, Guid? video = null) => new(title, null, false, null, null, video, video.HasValue);

    private async Task<Course> SeedAsync(bool complete = true, bool incomplete = false)
    {
        var course = Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Creator", "Publication course", "Private description", DateTimeOffset.UtcNow));
        if (complete || incomplete)
        {
            var module = course.AddModule(Changes("First"));
            if (complete) course.AddLesson(module, Changes("Lesson", Guid.CreateVersion7()));
            else
            {
                module = course.AddModule(Changes("Second")); course.AddLesson(module, Changes("Missing one")); course.AddLesson(module, Changes("Missing two"));
            }
        }
        await using var context = Context(); context.Courses.Add(course); await context.SaveChangesAsync(Cancellation); return course;
    }

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, Course course, string? key = null, string? note = null, int? revision = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/courses/{course.Id}/versions")
        { Content = JsonContent.Create(new { draftRevision = revision ?? course.DraftRevision, versionNote = note }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString());
        return client.SendAsync(request, Cancellation);
    }

    private async Task AssertCountsAsync(Course course, int versions, int messages)
    {
        await using var context = Context();
        Assert.Equal(versions, await context.CourseVersions.IgnoreQueryFilters().CountAsync(item => item.CourseId == course.Id, Cancellation));
        Assert.Equal(0, await context.OutboxMessages.IgnoreQueryFilters().CountAsync(item => item.TenantId == course.TenantId, Cancellation));
        Assert.Equal(messages, await context.ContentOutboxMessages.IgnoreQueryFilters().CountAsync(item => item.TenantId == course.TenantId, Cancellation));
        Assert.Equal(versions == 0 ? (int?)null : 1, await context.Courses.IgnoreQueryFilters().Where(item => item.Id == course.Id).Select(item => item.CurrentVersion).SingleAsync(Cancellation));
    }
}
