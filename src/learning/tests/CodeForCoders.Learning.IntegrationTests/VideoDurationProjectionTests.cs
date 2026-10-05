using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(VideoProjectionApiCollection.Name)]
[Trait("Layer", "Video duration projection - Integration")]
public sealed class VideoDurationProjectionTests(VideoProjectionApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private async Task<ProgressTestContext> SeedAsync()
    {
        var test = new ProgressTestContext(factory); await test.SeedAsync(); return test;
    }

    [Fact(DisplayName = nameof(DurationArrivingAfterProgressCompletesWithoutAnotherPlaybackFact))]
    public async Task DurationArrivingAfterProgressCompletesWithoutAnotherPlaybackFact()
    {
        var test = await SeedAsync(); await test.PublishAsync(test.Fact(540));
        Assert.Null((await test.ProgressAsync()).CompletedAt);
        await test.PublishDurationAsync(); Assert.NotNull((await test.ProgressAsync()).CompletedAt);
        await using var db = test.Context(); Assert.Equal(1, await db.PlaybackAdvances.CountAsync(Cancellation));
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(CodeForCoders.Learning.Infra.Data.VideoProjection.ProjectedVideo))!.GetProperties(),
            property => property.Name.Contains("Duration", StringComparison.Ordinal));
    }

    [Fact(DisplayName = nameof(HistoricalVideoReplayFillsDurationOutsideExistingContentReceipt))]
    public async Task HistoricalVideoReplayFillsDurationOutsideExistingContentReceipt()
    {
        var test = await SeedAsync(); var fact = new VideoAvailabilityFact(Guid.CreateVersion7(), test.TenantId, test.VideoId, test.Now.AddDays(-3), true, 600);
        // Represents a Content receipt made by the previous deployment, which did not store duration.
        await using (var db = test.Context())
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO content.video_fact_receipts (event_id, tenant_id, processed_at) VALUES ({fact.EventId}, {fact.TenantId}, {test.Now})", Cancellation);
        await test.PublishAsync(test.Fact(540)); await test.PublishVideoAsync(fact);
        await ProgressTestContext.EventuallyAsync(async () => (await test.ProgressAsync()).CompletedAt.HasValue);
        await test.ApplyVideoAsync(fact);
        await using var context = test.Context(); Assert.Equal(1, await context.VideoFactReceipts.CountAsync(Cancellation));
        Assert.Equal(600, (await context.VideoDurations.SingleAsync(Cancellation)).DurationSeconds);
        Assert.Empty(await context.ProjectedVideos.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(DurationUsesOccurrenceAndEventIdentityToPreventRegression))]
    public async Task DurationUsesOccurrenceAndEventIdentityToPreventRegression()
    {
        var test = await SeedAsync();
        var earlier = new VideoAvailabilityFact(Guid.CreateVersion7(test.Now), test.TenantId, test.VideoId, test.Now, true, 600);
        var newer = earlier with { EventId = Guid.CreateVersion7(test.Now.AddSeconds(1)), DurationSeconds = 700 };
        await test.PublishVideoAsync(newer); await test.PublishVideoAsync(earlier);
        await test.PublishVideoAsync(earlier with { EventId = Guid.CreateVersion7(test.Now.AddSeconds(2)), OccurredAt = test.Now.AddDays(-1), DurationSeconds = 800 });
        await test.PublishVideoAsync(newer with { EventId = Guid.CreateVersion7(), OccurredAt = test.Now.AddMinutes(1), DurationSeconds = 900 });
        await using var db = test.Context(); var duration = await db.VideoDurations.SingleAsync(Cancellation);
        Assert.Equal(900, duration.DurationSeconds); Assert.Equal(test.Now.AddMinutes(1), duration.OccurredAt);
    }

    [Fact(DisplayName = nameof(DurationOnlyCompletesCurrentLessonsUsingThatVideoInThatSchool))]
    public async Task DurationOnlyCompletesCurrentLessonsUsingThatVideoInThatSchool()
    {
        var test = await SeedAsync(); await test.PublishAsync(test.Fact(540));
        await using (var db = test.Context())
        {
            var course = await db.Courses.Include(item => item.Modules).ThenInclude(item => item.Lessons).SingleAsync(Cancellation);
            course.UpdateLesson(test.LessonId, new(null, null, false, null, null, Guid.CreateVersion7(), true));
            db.CourseVersions.Add(course.Publish(new(course.DraftRevision, null,
                new(test.TenantId, course.CreatedById, "Teacher", course.Title, null, test.Now.AddHours(1)))));
            await db.SaveChangesAsync(Cancellation);
        }
        await test.PublishDurationAsync(); Assert.Null((await test.ProgressAsync()).CompletedAt);
        var otherTenant = new VideoAvailabilityFact(Guid.CreateVersion7(), Guid.CreateVersion7(), test.VideoId, test.Now, true, 1);
        await test.ApplyVideoAsync(otherTenant); Assert.Null((await test.ProgressAsync()).CompletedAt);
    }

    [Fact(DisplayName = nameof(ConcurrentDurationAndProgressNeverMissCompletion))]
    public async Task ConcurrentDurationAndProgressNeverMissCompletion()
    {
        var test = await SeedAsync();
        await Task.WhenAll(test.ApplyAsync(test.Fact(540)), test.ApplyVideoAsync(new(Guid.CreateVersion7(), test.TenantId, test.VideoId, test.Now, true, 600)));
        Assert.NotNull((await test.ProgressAsync()).CompletedAt);
    }

    [Fact(DisplayName = nameof(FailureAndLargerDurationNeverClearCompletion))]
    public async Task FailureAndLargerDurationNeverClearCompletion()
    {
        var test = await SeedAsync(); await test.PublishDurationAsync(); await test.PublishAsync(test.Fact(540));
        var completedAt = (await test.ProgressAsync()).CompletedAt;
        await test.PublishVideoAsync(new(Guid.CreateVersion7(), test.TenantId, test.VideoId, test.Now.AddMinutes(1), false));
        await test.PublishVideoAsync(new(Guid.CreateVersion7(), test.TenantId, test.VideoId, test.Now.AddMinutes(2), true, 1200));
        Assert.Equal(completedAt, (await test.ProgressAsync()).CompletedAt);
    }
}
