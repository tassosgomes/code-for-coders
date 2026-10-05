using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using CodeForCoders.Learning.Infra.Data.Progress;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class ProgressTestContext(VideoProjectionApiFactory factory)
{
    public Guid TenantId { get; } = Guid.CreateVersion7();
    public Guid StudentId { get; } = Guid.CreateVersion7();
    public Guid VideoId { get; } = Guid.CreateVersion7();
    public Guid CourseId { get; private set; } = Guid.CreateVersion7();
    public Guid LessonId { get; private set; } = Guid.CreateVersion7();
    public DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public LearningDbContext Context()
    {
        var tenant = new TenantContext(); tenant.Set(TenantId);
        return new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, tenant);
    }

    public async Task SeedAsync()
    {
        using var client = factory.CreateClient();
        var actor = new CourseCreation(TenantId, Guid.CreateVersion7(), "Teacher", "Course", null, Now);
        var course = Course.Create(actor);
        var module = course.AddModule(new("Module", null, false, null, null));
        LessonId = course.AddLesson(module, new("Lesson", null, false, null, null, VideoId, true));
        CourseId = course.Id;
        await using var context = Context();
        context.Courses.Add(course);
        context.CourseVersions.Add(course.Publish(new(course.DraftRevision, null, actor)));
        await context.SaveChangesAsync(Cancellation);
    }

    public PlaybackProgressFact Fact(int position, string reason = "heartbeat", int sequence = 1)
        => new(Guid.CreateVersion7(), TenantId, Guid.CreateVersion7(), StudentId, CourseId, LessonId, sequence, position, reason, Now);

    public async Task PublishAsync(PlaybackProgressFact fact)
    {
        await PublishBodyAsync(PlaybackProgressFact.Route, fact.EventId, JsonSerializer.SerializeToUtf8Bytes(fact, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await EventuallyAsync(async () => { await using var db = Context(); return await db.PlaybackAdvances.AnyAsync(item => item.EventId == fact.EventId, Cancellation); });
        await WaitForQueueAsync("learning.playback-progress");
    }

    public async Task PublishVideoAsync(VideoAvailabilityFact fact)
    {
        await PublishBodyAsync(fact.IsReady ? VideoAvailabilityFact.ReadyRoute : VideoAvailabilityFact.FailedRoute, fact.EventId,
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                fact.EventId,
                fact.TenantId,
                fact.VideoId,
                fact.OccurredAt,
                durationSeconds = fact.DurationSeconds,
                reason = "invalid-video"
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await EventuallyAsync(async () => { await using var db = Context(); return await db.VideoFactReceipts.AnyAsync(item => item.EventId == fact.EventId, Cancellation); });
        await WaitForQueueAsync("learning.video-availability");
        if (fact.IsReady)
            await EventuallyAsync(async () => { await using var db = Context(); return await db.VideoDurations.AnyAsync(item => item.VideoId == fact.VideoId, Cancellation); });
    }

    public Task PublishDurationAsync(int seconds = 600) => PublishVideoAsync(new(Guid.CreateVersion7(), TenantId, VideoId, Now, true, seconds));

    public async Task PublishBodyAsync(string route, Guid eventId, byte[] body)
    {
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreatePublisherChannelAsync(Cancellation);
        await channel.BasicPublishAsync("media.events", route, true,
            new BasicProperties { MessageId = eventId.ToString(), Persistent = true }, body, Cancellation);
    }

    public async Task ApplyAsync(PlaybackProgressFact fact)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlaybackProgressStore>().ApplyAsync(fact, Cancellation);
    }

    public async Task ApplyVideoAsync(VideoAvailabilityFact fact)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VideoProjectionStore>().ApplyAsync(fact, Cancellation);
    }

    public async Task<LessonProgress> ProgressAsync()
    {
        await using var db = Context();
        return await db.LessonProgress.SingleAsync(item => item.StudentId == StudentId && item.LessonId == LessonId, Cancellation);
    }

    public async Task WaitForQueueAsync(string queue)
    {
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        await EventuallyAsync(async () => (await channel.QueueDeclarePassiveAsync(queue, Cancellation)).MessageCount == 0);
    }

    public static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (!await condition()) await Task.Delay(50, timeout.Token);
    }
}
