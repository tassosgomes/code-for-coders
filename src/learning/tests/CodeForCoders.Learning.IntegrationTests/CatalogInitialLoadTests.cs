using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Application.UseCases.Courses.PublishCourse;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.Catalog;
using CodeForCoders.Learning.Infra.Data.Outbox;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(LearningIntegrationCollection.Name)]
[Trait("Integration", "Catalog - Initial load")]
public sealed class CatalogInitialLoadTests(LearningIntegrationFixture infrastructure) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private readonly CatalogInitialLoadTestFixture fixture = new(infrastructure);

    public async ValueTask InitializeAsync() => await fixture.ResetAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory(DisplayName = nameof(DisabledOrAbsentFlagDoesNotReplayOrCompleteMarker))]
    [InlineData(null)]
    [InlineData("false")]
    public async Task DisabledOrAbsentFlagDoesNotReplayOrCompleteMarker(string? enabled)
    {
        await fixture.AddPublishedAsync(Guid.CreateVersion7());
        using var host = fixture.CreateHost(new Dictionary<string, string?> { ["CatalogInitialLoad:Enabled"] = enabled });
        await CatalogInitialLoadTestFixture.RunAsync(host);
        Assert.Empty(await fixture.ReplaysAsync());
        await using var context = fixture.Context();
        Assert.Empty(await context.CatalogInitialLoadExecutions.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(WorkerPublishesOneCurrentVersionPerCourseAcrossAllTenants))]
    public async Task WorkerPublishesOneCurrentVersionPerCourseAcrossAllTenants()
    {
        var first = await fixture.AddPublishedAsync(Guid.CreateVersion7());
        var second = await fixture.AddPublishedAsync(Guid.CreateVersion7());
        await fixture.AddDraftAsync(first.TenantId);
        await using (var context = fixture.Context())
        {
            var course = await context.Courses.IgnoreQueryFilters().Include(item => item.Modules).ThenInclude(module => module.Lessons)
                .SingleAsync(item => item.Id == first.CourseId, Cancellation);
            course.Update(new("Second published title", null, false, null, null));
            first = await CatalogInitialLoadTestFixture.PublishAsync(context, course);
        }
        using var host = fixture.EnabledHost();
        await using var queue = await fixture.BindQueueAsync(host);
        await host.StartAsync(Cancellation);
        try
        {
            var expected = new[] { first, second }.ToDictionary(version => version.Id);
            for (var index = 0; index < 2; index++)
            {
                var message = await fixture.ReceiveAsync(queue);
                Assert.Equal(PublishedCourseFact.Route, message.RoutingKey);
                using var body = JsonDocument.Parse(message.Body);
                PublishedCourseContract.AssertValid(body.RootElement);
                var id = body.RootElement.GetProperty("eventId").GetGuid();
                Assert.Equal(id.ToString(), message.BasicProperties.MessageId);
                Assert.True(expected.Remove(id, out var version));
                Assert.Equal(version!.TenantId, body.RootElement.GetProperty("tenantId").GetGuid());
                Assert.Equal(version.VersionNumber, body.RootElement.GetProperty("versionNumber").GetInt32());
            }
            Assert.Empty(expected);
            Assert.Null(await fixture.TryReceiveAsync(queue));
            Assert.Equal(2, (await fixture.ReplaysAsync()).Count);
        }
        finally { await host.StopAsync(Cancellation); }
    }

    [Fact(DisplayName = nameof(CompletedMarkerPreventsAnotherRunIncludingNewPublications))]
    public async Task CompletedMarkerPreventsAnotherRunIncludingNewPublications()
    {
        await fixture.AddPublishedAsync(Guid.CreateVersion7());
        using var host = fixture.EnabledHost();
        await CatalogInitialLoadTestFixture.RunAsync(host);
        var initial = Assert.Single(await fixture.ReplaysAsync());
        await fixture.AddPublishedAsync(Guid.CreateVersion7());
        using var restarted = fixture.EnabledHost();
        await CatalogInitialLoadTestFixture.RunAsync(restarted);
        Assert.Equal(initial.Id, Assert.Single(await fixture.ReplaysAsync()).Id);
        await using var context = fixture.Context();
        var marker = await context.CatalogInitialLoadExecutions.SingleAsync(Cancellation);
        Assert.Equal(CatalogInitialLoadExecution.ExecutionName, marker.Name);
        Assert.Equal(1, marker.CourseCount);
    }

    [Fact(DisplayName = nameof(ReplayUsesSnapshotWithoutReadingDraftOrOriginalOutboxPayload))]
    public async Task ReplayUsesSnapshotWithoutReadingDraftOrOriginalOutboxPayload()
    {
        var version = await fixture.AddPublishedAsync(Guid.CreateVersion7());
        await using (var context = fixture.Context())
        {
            var course = await context.Courses.IgnoreQueryFilters().SingleAsync(item => item.Id == version.CourseId, Cancellation);
            course.Update(new("Changed draft title", "Changed draft description", true, null, null,
                Level: "advanced", HasLevel: true, PrerequisiteText: "Changed draft prerequisite", HasPrerequisiteText: true));
            await context.SaveChangesAsync(Cancellation);
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE content.outbox_messages SET payload = '{{}}'::jsonb WHERE id = {version.Id}", Cancellation);
        }
        using var host = fixture.EnabledHost();
        await CatalogInitialLoadTestFixture.RunAsync(host);
        var replay = Assert.Single(await fixture.ReplaysAsync());
        using var body = JsonDocument.Parse(replay.Payload);
        PublishedCourseContract.AssertValid(body.RootElement);
        Assert.Equal(version.Title, body.RootElement.GetProperty("title").GetString());
        Assert.Equal(version.Description, body.RootElement.GetProperty("description").GetString());
        Assert.Equal("beginner", body.RootElement.GetProperty("level").GetString());
        Assert.Equal("Published prerequisite", body.RootElement.GetProperty("prerequisite").GetProperty("text").GetString());
        await using var read = fixture.Context();
        Assert.Equal("{}", (await read.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == version.Id, Cancellation)).Payload);
    }

    [Fact(DisplayName = nameof(LegacySnapshotReplaysExplicitNullLevelAndEmptyPrerequisite))]
    public async Task LegacySnapshotReplaysExplicitNullLevelAndEmptyPrerequisite()
    {
        await using var legacy = new CourseLevelLegacyFixture();
        await legacy.InitializeAsync(Cancellation);
        await using (var seed = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(legacy.ConnectionString).Options, new TenantContext()))
        {
            var modules = JsonSerializer.Serialize(new[] { new { moduleId = Guid.CreateVersion7(), title = "Legacy module", position = 1,
                lessons = new[] { new { lessonId = Guid.CreateVersion7(), title = "Legacy lesson", description = (string?)null, position = 1, videoId = Guid.CreateVersion7() } } } });
            await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE content.course_versions SET modules = {modules}::jsonb", Cancellation);
        }
        using var host = fixture.CreateHost(new Dictionary<string, string?>
        {
            ["CatalogInitialLoad:Enabled"] = "true",
            ["ConnectionStrings:DefaultConnection"] = legacy.ConnectionString
        });
        await CatalogInitialLoadTestFixture.RunAsync(host);
        await using var context = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(legacy.ConnectionString).Options, new TenantContext());
        var row = await context.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(Cancellation);
        using var body = JsonDocument.Parse(row.Payload);
        PublishedCourseContract.AssertValid(body.RootElement);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("level").ValueKind);
        var prerequisite = body.RootElement.GetProperty("prerequisite");
        Assert.Equal(JsonValueKind.Null, prerequisite.GetProperty("text").ValueKind);
        Assert.Empty(prerequisite.GetProperty("recommendedCourses").EnumerateArray());
        Assert.Equal(string.Empty, body.RootElement.GetProperty("description").GetString());
    }

    [Fact(DisplayName = nameof(NewPublicationKeepsOriginalMessageIdentityAndPayload))]
    public async Task NewPublicationKeepsOriginalMessageIdentityAndPayload()
    {
        var tenant = Guid.CreateVersion7();
        var course = await fixture.AddDraftAsync(tenant);
        using var host = fixture.CreateHost(new Dictionary<string, string?>());
        await using var queue = await fixture.BindQueueAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        var result = await scope.ServiceProvider.GetRequiredService<IPublishCourse>().ExecuteAsync(new(
            new CourseWriteContext(course.Id, tenant, Guid.CreateVersion7(), "Private teacher", Guid.CreateVersion7().ToString(), "{}"), course.DraftRevision, null), Cancellation);
        var context = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var row = await context.ContentOutboxMessages.SingleAsync(item => item.RoutingKey == PublishedCourseFact.Route, Cancellation);
        Assert.Equal(row.Id, row.MessageId);
        await host.Services.GetRequiredService<RabbitMqPublisher>().PublishAsync(row, Cancellation);
        var received = await fixture.ReceiveAsync(queue);
        using var body = JsonDocument.Parse(received.Body);
        PublishedCourseContract.AssertValid(body.RootElement);
        Assert.Equal(row.Id, body.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(row.Id.ToString(), received.BasicProperties.MessageId);
        Assert.Equal(result.VersionNumber, body.RootElement.GetProperty("versionNumber").GetInt32());
    }

    [Fact(DisplayName = nameof(MigrationBackfillsPendingContentOutboxAndPreservesDeliveryIdentity))]
    public async Task MigrationBackfillsPendingContentOutboxAndPreservesDeliveryIdentity()
    {
        await using var database = new PostgreSqlBuilder("postgres:18").Build();
        await database.StartAsync(Cancellation);
        await using var context = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(database.GetConnectionString()).Options, new TenantContext());
        await context.GetService<IMigrator>().MigrateAsync("20260930200410_AddCourseVersionAudience", Cancellation);
        var id = Guid.CreateVersion7(); var tenant = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(new { eventId = id });
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content.outbox_messages (id, tenant_id, type, routing_key, payload, occurred_on, attempts)
            VALUES ({id}, {tenant}, {PublishedCourseFact.Route}, {PublishedCourseFact.Route}, {payload}::jsonb, {now}, 0)
            """, Cancellation);
        await context.Database.MigrateAsync(Cancellation);
        var row = await context.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(Cancellation);
        Assert.Equal(id, row.MessageId); Assert.Null(row.ProcessedOn);
        Assert.Equal(id, JsonDocument.Parse(row.Payload).RootElement.GetProperty("eventId").GetGuid());
        using var host = fixture.EnabledHost();
        await using var queue = await fixture.BindQueueAsync(host);
        await host.Services.GetRequiredService<RabbitMqPublisher>().PublishAsync(row, Cancellation);
        Assert.Equal(id.ToString(), (await fixture.ReceiveAsync(queue)).BasicProperties.MessageId);
    }

    [Fact(DisplayName = nameof(ProgressOutboxStillUsesItsOwnRowIdentity))]
    public async Task ProgressOutboxStillUsesItsOwnRowIdentity()
    {
        using var host = fixture.EnabledHost();
        await using var queue = await fixture.BindQueueAsync(host);
        var id = Guid.CreateVersion7();
        var row = OutboxMessage.Create(new(id, Guid.CreateVersion7(), PublishedCourseFact.Route, PublishedCourseFact.Route, new { eventId = id }, DateTimeOffset.UtcNow, null), "{}");
        await host.Services.GetRequiredService<RabbitMqPublisher>().PublishAsync(row, Cancellation);
        Assert.Equal(id.ToString(), (await fixture.ReceiveAsync(queue)).BasicProperties.MessageId);
    }

    [Fact(DisplayName = nameof(ConcurrentHostsReplayEachCourseOnlyOnce))]
    public async Task ConcurrentHostsReplayEachCourseOnlyOnce()
    {
        await fixture.AddPublishedAsync(Guid.CreateVersion7());
        using var first = fixture.EnabledHost(); using var second = fixture.EnabledHost();
        await Task.WhenAll(CatalogInitialLoadTestFixture.RunAsync(first), CatalogInitialLoadTestFixture.RunAsync(second));
        Assert.Single(await fixture.ReplaysAsync());
        await using var context = fixture.Context();
        Assert.Single(await context.CatalogInitialLoadExecutions.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ReplayLeavesOriginalRowUntouchedAndDoesNotCreateAuditAct))]
    public async Task ReplayLeavesOriginalRowUntouchedAndDoesNotCreateAuditAct()
    {
        var version = await fixture.AddPublishedAsync(Guid.CreateVersion7());
        await using var before = fixture.Context();
        var original = await before.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == version.Id, Cancellation);
        using var host = fixture.EnabledHost();
        await CatalogInitialLoadTestFixture.RunAsync(host);
        var replay = Assert.Single(await fixture.ReplaysAsync());
        Assert.NotEqual(original.Id, replay.Id);
        Assert.Equal(original.MessageId, replay.MessageId);
        await using var read = fixture.Context();
        var preserved = await read.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == version.Id, Cancellation);
        Assert.Equal(original.Payload, preserved.Payload);
        Assert.Equal(original.ProcessedOn, preserved.ProcessedOn);
        Assert.Equal(original.Attempts, preserved.Attempts);
        Assert.False(await read.ContentOutboxMessages.IgnoreQueryFilters().AnyAsync(item => item.RoutingKey == "auditoria.ato-praticado.v1", Cancellation));
    }

    [Fact(DisplayName = nameof(EmptyCatalogStillCompletesOnce))]
    public async Task EmptyCatalogStillCompletesOnce()
    {
        using var host = fixture.EnabledHost();
        await CatalogInitialLoadTestFixture.RunAsync(host);
        await using var context = fixture.Context();
        Assert.Equal(0, (await context.CatalogInitialLoadExecutions.SingleAsync(Cancellation)).CourseCount);
        Assert.Empty(await fixture.ReplaysAsync());
    }
}
