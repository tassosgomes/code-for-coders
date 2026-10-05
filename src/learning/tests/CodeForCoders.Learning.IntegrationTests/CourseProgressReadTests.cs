using System.Net;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(StudentLessonCollection.Name)]
public sealed class CourseProgressReadTests(StudentLessonApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static async Task<JsonDocument> BodyAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));

    [Theory(DisplayName = nameof(PercentUsesFloorAndOnlyRecordedLessonsAreReturned))]
    [InlineData(8, 3, 37)]
    [InlineData(3, 2, 66)]
    [InlineData(2, 0, 0)]
    public async Task PercentUsesFloorAndOnlyRecordedLessonsAreReturned(int total, int completed, int percent)
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(total, completed);
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response); var root = body.RootElement;
        Assert.Equal(fixture.Course.Id, root.GetProperty("courseId").GetGuid());
        Assert.Equal(total, root.GetProperty("totalLessons").GetInt32()); Assert.Equal(completed, root.GetProperty("completedLessons").GetInt32());
        Assert.Equal(percent, root.GetProperty("percent").GetInt32()); Assert.Equal(completed, root.GetProperty("lessons").GetArrayLength());
        Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        Assert.Contains($"studentId={fixture.StudentId:D}", factory.Commerce.Path!);
    }

    [Fact(DisplayName = nameof(RepublishingTenCompletedLessonsWithTwoNewLessonsProduces83Percent))]
    public async Task RepublishingTenCompletedLessonsWithTwoNewLessonsProduces83Percent()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(10, 10);
        await fixture.RepublishAsync(course =>
        {
            for (var i = 0; i < 2; i++) course.AddLesson(course.Modules[0].Id, new("New lesson", null, false, null, null, Guid.CreateVersion7(), true));
        });
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response); Assert.Equal(83, body.RootElement.GetProperty("percent").GetInt32());
        Assert.Equal(12, body.RootElement.GetProperty("totalLessons").GetInt32()); Assert.Equal(2, body.RootElement.GetProperty("versionNumber").GetInt32());
    }

    [Fact(DisplayName = nameof(RemovedCompletedLessonIsExcludedAndCountsAgainWhenRestored))]
    public async Task RemovedCompletedLessonIsExcludedAndCountsAgainWhenRestored()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(3, 1); var original = fixture.Version;
        var lesson = fixture.Lessons[0].LessonId; await fixture.RepublishAsync(course => course.RemoveLesson(lesson));
        using var client = fixture.Client();
        using (var response = await fixture.GetAsync(client))
        {
            response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
            Assert.Equal(2, body.RootElement.GetProperty("totalLessons").GetInt32()); Assert.Equal(0, body.RootElement.GetProperty("completedLessons").GetInt32());
            Assert.Empty(body.RootElement.GetProperty("lessons").EnumerateArray());
        }
        await fixture.RepublishAsync(course => course.DiscardDraft(course.DraftRevision, original));
        using var restored = await fixture.GetAsync(client); restored.EnsureSuccessStatusCode(); using var result = await BodyAsync(restored);
        Assert.Equal(3, result.RootElement.GetProperty("totalLessons").GetInt32()); Assert.Equal(1, result.RootElement.GetProperty("completedLessons").GetInt32());
        Assert.Equal(lesson, result.RootElement.GetProperty("lessons")[0].GetProperty("lessonId").GetGuid());
    }

    [Theory(DisplayName = nameof(ResumeUsesCurrentVideoDurationAndLastAdvanceReason))]
    [InlineData(252, "ended", 600, 0)]
    [InlineData(591, "paused", 600, 0)]
    [InlineData(601, "paused", 600, 0)]
    [InlineData(590, "paused", 600, 590)]
    [InlineData(252, "paused", 0, 252)]
    [InlineData(252, "ended", 0, 0)]
    public async Task ResumeUsesCurrentVideoDurationAndLastAdvanceReason(int position, string reason, int duration, int resume)
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(); var lesson = fixture.Lessons[0];
        await fixture.ProgressAsync(lesson.LessonId, position, reason, true);
        if (duration > 0) await fixture.DurationAsync(lesson.VideoId, duration);
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); response.EnsureSuccessStatusCode();
        using var body = await BodyAsync(response); var progress = body.RootElement.GetProperty("lessons")[0];
        Assert.Equal(position, progress.GetProperty("lastPositionSeconds").GetInt32()); Assert.Equal(resume, progress.GetProperty("resumeAtSeconds").GetInt32());
        Assert.True(progress.GetProperty("completed").GetBoolean());
    }

    [Fact(DisplayName = nameof(ReplacingVideoUsesItsNewDurationWithoutLosingCompletion))]
    public async Task ReplacingVideoUsesItsNewDurationWithoutLosingCompletion()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(2); var lesson = fixture.Lessons[0];
        await fixture.ProgressAsync(lesson.LessonId, 252, completed: true); await fixture.DurationAsync(lesson.VideoId, 600);
        var newVideo = Guid.CreateVersion7(); await fixture.DurationAsync(newVideo, 200);
        await fixture.RepublishAsync(course => course.UpdateLesson(lesson.LessonId, new(null, null, false, null, null, newVideo, true)));
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        Assert.Equal(0, body.RootElement.GetProperty("lessons")[0].GetProperty("resumeAtSeconds").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("completedLessons").GetInt32());
    }

    [Fact(DisplayName = nameof(OtherStudentProgressCannotBeReadByOverridingQuerySubject))]
    public async Task OtherStudentProgressCannotBeReadByOverridingQuerySubject()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(2);
        await fixture.ProgressAsync(fixture.Lessons[0].LessonId, 252, completed: true, student: Guid.CreateVersion7());
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); response.EnsureSuccessStatusCode(); using var body = await BodyAsync(response);
        Assert.Equal(0, body.RootElement.GetProperty("completedLessons").GetInt32()); Assert.Empty(body.RootElement.GetProperty("lessons").EnumerateArray());
    }

    [Theory(DisplayName = nameof(DeniedStudentsReceiveNoCourseData))]
    [InlineData("no-grant")]
    [InlineData("grant-ended")]
    public async Task DeniedStudentsReceiveNoCourseData(string reason)
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(2, 1); var ended = DateTimeOffset.UtcNow.AddDays(-1);
        factory.Commerce.Decision = new("denied", null, reason, reason == "grant-ended" ? ended : null, DateTimeOffset.UtcNow);
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); await ErrorAsync(response, 403, "ACCESS_DENIED");
        using var body = await BodyAsync(response); Assert.Equal(reason, body.RootElement.GetProperty("reason").GetString());
        if (reason == "grant-ended") Assert.Equal(ended, body.RootElement.GetProperty("accessEndedAt").GetDateTimeOffset());
    }

    [Theory(DisplayName = nameof(UnavailableCommerceProduces503))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableCommerceProduces503(bool timeout)
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(); factory.Commerce.Status = HttpStatusCode.InternalServerError; factory.Commerce.Timeout = timeout;
        using var client = fixture.Client(); using var response = await fixture.GetAsync(client); await ErrorAsync(response, 503, "ACCESS_DECISION_UNAVAILABLE");
    }

    [Theory(DisplayName = nameof(UnavailableCoursesReturnIdentical404BeforeAccessDecision))]
    [InlineData("missing")]
    [InlineData("other-school")]
    [InlineData("draft")]
    public async Task UnavailableCoursesReturnIdentical404BeforeAccessDecision(string scenario)
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(published: scenario != "draft"); var calls = factory.Commerce.Calls;
        using var client = scenario == "other-school" ? factory.Student(Guid.CreateVersion7(), fixture.StudentId) : fixture.Client();
        using var response = await fixture.GetAsync(client, scenario == "missing" ? Guid.CreateVersion7() : fixture.Course.Id);
        await ErrorAsync(response, 404, "COURSE_NOT_AVAILABLE"); Assert.Equal(calls, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(ActorTokenIsRejectedWith401BeforeAccessDecision))]
    public async Task ActorTokenIsRejectedWith401BeforeAccessDecision()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(); var calls = factory.Commerce.Calls;
        using var client = factory.Student(fixture.Course.TenantId, fixture.StudentId, actor: true, audience: "actor");
        using var response = await fixture.GetAsync(client); await ErrorAsync(response, 401, "TOKEN_INVALID"); Assert.Equal(calls, factory.Commerce.Calls);
    }
    private static async Task ErrorAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode); using var body = await BodyAsync(response);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("courseId", out _)); Assert.False(body.RootElement.TryGetProperty("lessons", out _));
        Assert.DoesNotContain("Private", await response.Content.ReadAsStringAsync(Cancellation));
    }
}
