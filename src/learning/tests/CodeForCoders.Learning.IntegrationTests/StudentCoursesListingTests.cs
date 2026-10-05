using System.Net;
using System.Text.Json;
using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(StudentLessonCollection.Name)]
public sealed class StudentCoursesListingTests(StudentLessonApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-04T14:00:00Z");
    private CourseProgressReadTestFixture Fixture() => new(factory);

    private static StudentCourseAccess Active(CourseProgressReadTestFixture fixture, DateTimeOffset? since = null)
        => new(fixture.Course.Id, "active", since ?? Now, null, null, null);
    private async Task<JsonDocument> GetAsync(CourseProgressReadTestFixture fixture)
    {
        using var client = fixture.Client();
        using var response = await client.GetAsync($"/internal/v1/student-courses?studentId={Guid.CreateVersion7()}", Cancellation);
        response.EnsureSuccessStatusCode(); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        Assert.Contains($"studentId={fixture.StudentId:D}", factory.Commerce.Path!);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(ContinueUsesCurrentCurriculumAndMostRecentActivity))]
    [InlineData("incomplete", 1, 0)]
    [InlineData("next", 2, 1)]
    [InlineData("wrap", 0, 1)]
    [InlineData("all-completed", 2, 3)]
    [InlineData("removed", 0, 0)]
    [InlineData("never-started", 0, 0)]
    public async Task ContinueUsesCurrentCurriculumAndMostRecentActivity(string scenario, int expected, int completed)
    {
        var fixture = Fixture(); await fixture.SeedAsync(3); var lessons = fixture.Lessons.ToArray();
        if (scenario == "incomplete") await fixture.ProgressAsync(lessons[1].LessonId, 252, activity: Now);
        if (scenario == "next") await fixture.ProgressAsync(lessons[1].LessonId, 550, completed: true, activity: Now);
        if (scenario == "wrap") await fixture.ProgressAsync(lessons[2].LessonId, 550, completed: true, activity: Now);
        if (scenario == "all-completed")
            for (var i = 0; i < 3; i++) await fixture.ProgressAsync(lessons[i].LessonId, 550, completed: true, activity: Now.AddSeconds(i));
        if (scenario == "removed")
        {
            await fixture.ProgressAsync(lessons[1].LessonId, 252, completed: true, activity: Now);
            await fixture.RepublishAsync(course => course.RemoveLesson(lessons[1].LessonId));
        }
        factory.Commerce.CourseAccess = new([Active(fixture)]);
        using var body = await GetAsync(fixture); var root = body.RootElement; var course = Assert.Single(root.GetProperty("active").EnumerateArray());
        Assert.True(root.GetProperty("progressAvailable").GetBoolean()); Assert.Empty(root.GetProperty("ended").EnumerateArray());
        Assert.Equal(lessons[expected].LessonId, course.GetProperty("continueLessonId").GetGuid());
        Assert.Equal(scenario != "never-started", course.GetProperty("started").GetBoolean());
        Assert.Equal(completed, course.GetProperty("progress").GetProperty("completedLessons").GetInt32());
        Assert.Equal(scenario == "removed" ? 2 : 3, course.GetProperty("progress").GetProperty("totalLessons").GetInt32());
    }

    [Fact(DisplayName = nameof(ActiveCoursesOrderByLastActivityThenNeverStartedBySince))]
    public async Task ActiveCoursesOrderByLastActivityThenNeverStartedBySince()
    {
        var started = Fixture(); await started.SeedAsync();
        var latest = Fixture(); await latest.SeedAsync(tenantId: started.Course.TenantId);
        var newGrant = Fixture(); await newGrant.SeedAsync(tenantId: started.Course.TenantId);
        var oldGrant = Fixture(); await oldGrant.SeedAsync(tenantId: started.Course.TenantId);
        await started.ProgressAsync(started.Lessons[0].LessonId, 252, activity: Now.AddHours(-1));
        await latest.ProgressAsync(latest.Lessons[0].LessonId, 252, student: started.StudentId, activity: Now);
        factory.Commerce.CourseAccess = new([Active(oldGrant, Now.AddDays(-2)), Active(newGrant, Now),
            Active(started, Now.AddDays(1)), Active(latest, Now.AddDays(-1))]);
        using var body = await GetAsync(started);
        Assert.Equal([latest.Course.Id, started.Course.Id, newGrant.Course.Id, oldGrant.Course.Id],
            body.RootElement.GetProperty("active").EnumerateArray().Select(item => item.GetProperty("courseId").GetGuid()));
    }

    [Fact(DisplayName = nameof(CourseWithoutCurrentVersionIsOmitted))]
    public async Task CourseWithoutCurrentVersionIsOmitted()
    {
        var fixture = Fixture(); await fixture.SeedAsync(published: false); factory.Commerce.CourseAccess = new([Active(fixture)]);
        using var body = await GetAsync(fixture); Assert.Empty(body.RootElement.GetProperty("active").EnumerateArray());
    }

    [Fact(DisplayName = nameof(ProgressNeverAddsCoursesWithoutAccess))]
    public async Task ProgressNeverAddsCoursesWithoutAccess()
    {
        var fixture = Fixture(); await fixture.SeedAsync(completed: 3);
        factory.Commerce.CourseAccess = new([]);
        using var body = await GetAsync(fixture); Assert.Empty(body.RootElement.GetProperty("active").EnumerateArray());
        Assert.Empty(body.RootElement.GetProperty("ended").EnumerateArray());
    }

    [Fact(DisplayName = nameof(TitleAndCountsComeFromCurrentPublishedVersion))]
    public async Task TitleAndCountsComeFromCurrentPublishedVersion()
    {
        var fixture = Fixture(); await fixture.SeedAsync(8, 3);
        await fixture.RepublishAsync(course => course.Update(new("Published title", null, false, null, null)));
        await using (var db = fixture.Context())
        {
            var tracked = await db.Courses.SingleAsync(course => course.Id == fixture.Course.Id, Cancellation);
            tracked.Update(new("Unpublished draft", null, false, null, null)); await db.SaveChangesAsync(Cancellation);
        }
        factory.Commerce.CourseAccess = new([Active(fixture)]);
        using var body = await GetAsync(fixture); var course = body.RootElement.GetProperty("active")[0];
        Assert.Equal("Published title", course.GetProperty("title").GetString());
        Assert.Equal(37, course.GetProperty("progress").GetProperty("percent").GetInt32());
        Assert.Equal(3, course.GetProperty("progress").GetProperty("completedLessons").GetInt32());
    }

    [Fact(DisplayName = nameof(OtherStudentProgressIsNotRead))]
    public async Task OtherStudentProgressIsNotRead()
    {
        var fixture = Fixture(); await fixture.SeedAsync(3);
        await fixture.ProgressAsync(fixture.Lessons[1].LessonId, 550, completed: true, student: Guid.CreateVersion7());
        factory.Commerce.CourseAccess = new([Active(fixture)]); using var body = await GetAsync(fixture);
        Assert.False(body.RootElement.GetProperty("active")[0].GetProperty("started").GetBoolean());
        Assert.Equal(0, body.RootElement.GetProperty("active")[0].GetProperty("progress").GetProperty("percent").GetInt32());
    }

    [Theory(DisplayName = nameof(CommerceFailureReturns503AndNeverAnEmptyList))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommerceFailureReturns503AndNeverAnEmptyList(bool timeout)
    {
        var fixture = Fixture(); await fixture.SeedAsync(); factory.Commerce.Timeout = timeout; factory.Commerce.Status = HttpStatusCode.Forbidden;
        using var client = fixture.Client(); using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("COURSE_ACCESS_UNAVAILABLE", body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("active", out _));
    }

    [Fact(DisplayName = nameof(EndedCoursesKeepOnlyPublicSummaryAndDate))]
    public async Task EndedCoursesKeepOnlyPublicSummaryAndDate()
    {
        var fixture = Fixture(); await fixture.SeedAsync(8, 3);
        factory.Commerce.CourseAccess = new([new(fixture.Course.Id, "ended", null, new(2026, 10, 1), Now.AddDays(-3), "grant-ended")]);
        using var body = await GetAsync(fixture); Assert.Empty(body.RootElement.GetProperty("active").EnumerateArray());
        var course = Assert.Single(body.RootElement.GetProperty("ended").EnumerateArray());
        Assert.Equal(["courseId", "title", "endedOn", "endedReason", "progress"], course.EnumerateObject().Select(item => item.Name));
        Assert.Equal("2026-10-01", course.GetProperty("endedOn").GetString());
        Assert.Equal(37, course.GetProperty("progress").GetProperty("percent").GetInt32());
    }

    [Fact(DisplayName = nameof(OtherSchoolCannotReadPublishedCourse))]
    public async Task OtherSchoolCannotReadPublishedCourse()
    {
        var fixture = Fixture(); await fixture.SeedAsync(); factory.Commerce.CourseAccess = new([Active(fixture)]);
        using var client = factory.Student(Guid.CreateVersion7(), fixture.StudentId);
        using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation); response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Empty(body.RootElement.GetProperty("active").EnumerateArray());
    }

    [Fact(DisplayName = nameof(AnonymousRequestNeverCallsCommerce))]
    public async Task AnonymousRequestNeverCallsCommerce()
    {
        using var client = factory.CreateClient(); var before = factory.Commerce.Calls;
        using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(before, factory.Commerce.Calls);
    }
}
