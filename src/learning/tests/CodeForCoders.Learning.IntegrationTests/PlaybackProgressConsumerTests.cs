using System.Text.Json;
using CodeForCoders.ContractTesting;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(VideoProjectionApiCollection.Name)]
[Trait("Layer", "Playback progress consumer - Integration")]
public sealed class PlaybackProgressConsumerTests(VideoProjectionApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private async Task<ProgressTestContext> SeedAsync()
    {
        var test = new ProgressTestContext(factory); await test.SeedAsync(); return test;
    }

    [Fact(DisplayName = nameof(DuplicateEventPreservesRawFactAndAllProgressFields))]
    public async Task DuplicateEventPreservesRawFactAndAllProgressFields()
    {
        var test = await SeedAsync(); var fact = test.Fact(300);
        await test.PublishAsync(fact); var before = await test.ProgressAsync();
        await test.PublishAsync(fact with { PositionSeconds = 600, Reason = "ended" });
        // Direct application also waits for the duplicate transaction; queue depth excludes in-flight deliveries.
        await test.ApplyAsync(fact with { PositionSeconds = 600, Reason = "ended" });
        var after = await test.ProgressAsync();
        Assert.Equal(before.LastPositionSeconds, after.LastPositionSeconds);
        Assert.Equal(before.MaxPositionSeconds, after.MaxPositionSeconds);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.LastActivityAt, after.LastActivityAt);
        await using var db = test.Context();
        Assert.Equal(1, await db.PlaybackAdvances.CountAsync(Cancellation));
        Assert.Equal(300, (await db.PlaybackAdvances.SingleAsync(Cancellation)).PositionSeconds);
    }

    [Fact(DisplayName = nameof(OutOfOrderDeliveryCannotRegressLastPosition))]
    public async Task OutOfOrderDeliveryCannotRegressLastPosition()
    {
        var test = await SeedAsync();
        await test.PublishAsync(test.Fact(480) with { OccurredAt = test.Now.AddMinutes(10) });
        await test.PublishAsync(test.Fact(252) with { OccurredAt = test.Now.AddMinutes(2) });
        var progress = await test.ProgressAsync();
        Assert.Equal(480, progress.LastPositionSeconds); Assert.Equal(480, progress.MaxPositionSeconds);
        Assert.Equal(test.Now.AddMinutes(10), progress.LastActivityAt);
    }

    [Fact(DisplayName = nameof(NewerDeviceMayResumeEarlierWhileMaximumIsPreserved))]
    public async Task NewerDeviceMayResumeEarlierWhileMaximumIsPreserved()
    {
        var test = await SeedAsync();
        await test.PublishAsync(test.Fact(300, "paused", 90));
        await test.PublishAsync(test.Fact(120, "left") with { OccurredAt = test.Now.AddHours(1) });
        var progress = await test.ProgressAsync();
        Assert.Equal(120, progress.LastPositionSeconds); Assert.Equal(300, progress.MaxPositionSeconds);
        Assert.Equal("left", progress.Reason); Assert.Equal(1, progress.Sequence);
    }

    [Theory(DisplayName = nameof(NinetyPercentCompletesButLowerPositionDoesNot))]
    [InlineData(530, false)]
    [InlineData(540, true)]
    public async Task NinetyPercentCompletesButLowerPositionDoesNot(int position, bool completed)
    {
        var test = await SeedAsync(); await test.PublishDurationAsync(); await test.PublishAsync(test.Fact(position));
        Assert.Equal(completed, (await test.ProgressAsync()).CompletedAt.HasValue);
    }

    [Fact(DisplayName = nameof(EndedCompletesWithoutKnownDuration))]
    public async Task EndedCompletesWithoutKnownDuration()
    {
        var test = await SeedAsync(); await test.PublishAsync(test.Fact(12, "ended"));
        Assert.NotNull((await test.ProgressAsync()).CompletedAt);
    }

    [Fact(DisplayName = nameof(ReplayAtMinuteTwoNeverUndoesCompletion))]
    public async Task ReplayAtMinuteTwoNeverUndoesCompletion()
    {
        var test = await SeedAsync(); await test.PublishDurationAsync(); await test.PublishAsync(test.Fact(540));
        var completion = (await test.ProgressAsync()).CompletedAt;
        await test.PublishAsync(test.Fact(120, "paused", 2) with { OccurredAt = test.Now.AddMinutes(1) });
        var progress = await test.ProgressAsync();
        Assert.Equal(completion, progress.CompletedAt); Assert.Equal(120, progress.LastPositionSeconds);
    }

    [Fact(DisplayName = nameof(UnknownReasonIsRetainedAndDoesNotImplyCompletion))]
    public async Task UnknownReasonIsRetainedAndDoesNotImplyCompletion()
    {
        var test = await SeedAsync(); await test.PublishAsync(test.Fact(100, "future-player-reason"));
        var progress = await test.ProgressAsync();
        Assert.Equal("future-player-reason", progress.Reason); Assert.Null(progress.CompletedAt);
        await using var db = test.Context(); Assert.Equal("future-player-reason", (await db.PlaybackAdvances.SingleAsync(Cancellation)).Reason);
    }

    [Fact(DisplayName = nameof(RemovedLessonIsRecordedWithoutUsingHistoricalVideoDuration))]
    public async Task RemovedLessonIsRecordedWithoutUsingHistoricalVideoDuration()
    {
        var test = await SeedAsync(); await test.PublishDurationAsync();
        await using (var db = test.Context())
        {
            var course = await db.Courses.Include(item => item.Modules).ThenInclude(item => item.Lessons).SingleAsync(Cancellation);
            course.AddLesson(course.Modules[0].Id, new("Replacement", null, false, null, null, Guid.CreateVersion7(), true));
            course.RemoveLesson(test.LessonId);
            db.CourseVersions.Add(course.Publish(new(course.DraftRevision, null,
                new(test.TenantId, course.CreatedById, "Teacher", course.Title, null, test.Now.AddHours(1)))));
            await db.SaveChangesAsync(Cancellation);
        }
        await test.PublishAsync(test.Fact(600)); Assert.Null((await test.ProgressAsync()).CompletedAt);
        await using var context = test.Context(); Assert.Equal(1, await context.PlaybackAdvances.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(UnknownCourseIsRecordedWithoutCreatingContent))]
    public async Task UnknownCourseIsRecordedWithoutCreatingContent()
    {
        using var client = factory.CreateClient();
        var test = new ProgressTestContext(factory); await test.PublishAsync(test.Fact(600));
        Assert.Null((await test.ProgressAsync()).CompletedAt);
        await using var db = test.Context(); Assert.Empty(await db.Courses.ToListAsync(Cancellation));
        Assert.Empty(await db.CourseVersions.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConsumedPayloadMatchesCurrentMediaContractAndRawStorageHasOnlyFactFields))]
    public async Task ConsumedPayloadMatchesCurrentMediaContractAndRawStorageHasOnlyFactFields()
    {
        var test = await SeedAsync(); var fact = test.Fact(252, "paused", 5);
        var json = JsonSerializer.Serialize(fact, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        AsyncApiContract.Load("media/asyncapi.yaml").AssertSends(PlaybackProgressFact.Route, json);
        await test.PublishAsync(fact);
        await using var db = test.Context(); var raw = await db.PlaybackAdvances.SingleAsync(Cancellation);
        Assert.Equal(fact.SessionId, raw.SessionId); Assert.Equal(fact.StudentId, raw.StudentId);
        Assert.Equal(fact.CourseId, raw.CourseId); Assert.Equal(fact.LessonId, raw.LessonId);
        Assert.Equal(fact.EventId, raw.EventId); Assert.Equal(fact.Sequence, raw.Sequence);
        Assert.Equal(fact.PositionSeconds, raw.PositionSeconds); Assert.Equal(fact.Reason, raw.Reason);
        Assert.Equal(fact.OccurredAt, raw.OccurredAt); Assert.True(raw.ReceivedAt >= raw.OccurredAt);
        var fields = db.Model.FindEntityType(raw.GetType())!.GetProperties().Select(item => item.GetColumnName()).Order().ToArray();
        Assert.Equal(new[] { "event_id", "tenant_id", "session_id", "student_id", "course_id", "lesson_id", "sequence",
            "position_seconds", "reason", "occurred_at", "received_at" }.Order(), fields);
    }

    [Fact(DisplayName = nameof(SequenceBreaksOccurrenceTie))]
    public async Task SequenceBreaksOccurrenceTie()
    {
        var test = await SeedAsync(); await test.PublishAsync(test.Fact(300, "left", 4));
        await test.PublishAsync(test.Fact(120, "paused", 5)); await test.PublishAsync(test.Fact(600, "heartbeat", 3));
        var progress = await test.ProgressAsync(); Assert.Equal(120, progress.LastPositionSeconds);
        Assert.Equal(5, progress.Sequence); Assert.Equal("paused", progress.Reason); Assert.Equal(600, progress.MaxPositionSeconds);
    }

    [Fact(DisplayName = nameof(ConcurrentConsumersKeepNewestPairMaximumAndCompletion))]
    public async Task ConcurrentConsumersKeepNewestPairMaximumAndCompletion()
    {
        var test = await SeedAsync(); await test.PublishDurationAsync();
        await Task.WhenAll(Enumerable.Range(1, 12).Select(sequence => test.ApplyAsync(test.Fact(sequence == 1 ? 600 : 120, "paused", sequence)
            with
        { OccurredAt = test.Now.AddSeconds(sequence) })));
        var progress = await test.ProgressAsync(); Assert.Equal(12, progress.Sequence);
        Assert.Equal(120, progress.LastPositionSeconds); Assert.Equal(600, progress.MaxPositionSeconds); Assert.NotNull(progress.CompletedAt);
        await using var db = test.Context(); Assert.Equal(12, await db.PlaybackAdvances.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(TenantMismatchDoesNotUseOtherSchoolsCurriculumOrDuration))]
    public async Task TenantMismatchDoesNotUseOtherSchoolsCurriculumOrDuration()
    {
        var test = await SeedAsync(); await test.PublishDurationAsync();
        var fact = test.Fact(600) with { TenantId = Guid.CreateVersion7() }; await test.ApplyAsync(fact);
        await using var db = test.Context();
        Assert.Empty(await db.PlaybackAdvances.ToListAsync(Cancellation));
        Assert.Null((await db.LessonProgress.IgnoreQueryFilters().SingleAsync(item => item.TenantId == fact.TenantId, Cancellation)).CompletedAt);
    }

    [Theory(DisplayName = nameof(InvalidPayloadGoesToQuorumDeadLetterQueueWithoutRawRecord))]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("position")]
    [InlineData("personal-field")]
    public async Task InvalidPayloadGoesToQuorumDeadLetterQueueWithoutRawRecord(string scenario)
    {
        var test = await SeedAsync(); var fact = test.Fact(30);
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(fact, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!.AsObject();
        if (scenario == "missing") node.Remove("studentId");
        if (scenario == "position") node["positionSeconds"] = -1;
        if (scenario == "personal-field") node["email"] = "private@example.test";
        var envelope = scenario == "identity" ? Guid.CreateVersion7() : fact.EventId;
        await test.PublishBodyAsync(PlaybackProgressFact.Route, envelope, JsonSerializer.SerializeToUtf8Bytes(node));
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        BasicGetResult? rejected = null;
        await ProgressTestContext.EventuallyAsync(async () => { rejected = await channel.BasicGetAsync("learning.playback-progress.dlq", true, Cancellation); return rejected is not null; });
        Assert.Equal(envelope.ToString(), rejected!.BasicProperties.MessageId);
        await using var db = test.Context(); Assert.Empty(await db.PlaybackAdvances.ToListAsync(Cancellation));
    }
}
