using System.Net;
using System.Text.Json;
using CodeForCoders.Learning.Application.Interfaces;
using Npgsql;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(StudentLessonCollection.Name)]
[Trait("Integration", "StudentCourses - Degradation")]
public sealed class StudentCoursesDegradationTests(StudentLessonApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-04T14:00:00Z");

    private static StudentCourseAccess Ended(CourseProgressReadTestFixture fixture, DateTimeOffset? endedAt = null)
        => new(fixture.Course.Id, "ended", null, new(2026, 10, 1), endedAt ?? Now, "grant-ended");

    private async Task<JsonDocument> GetAsync(CourseProgressReadTestFixture fixture)
    {
        using var client = fixture.Client();
        using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ExpiredCourtesyKeepsCurrentTitleDateAndHalfCompletedProgress))]
    public async Task ExpiredCourtesyKeepsCurrentTitleDateAndHalfCompletedProgress()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(10, 5);
        await fixture.RepublishAsync(course => course.Update(new("Current published title", null, false, null, null)));
        factory.Commerce.CourseAccess = new([Ended(fixture)]);
        using var body = await GetAsync(fixture);
        Assert.True(body.RootElement.GetProperty("progressAvailable").GetBoolean());
        Assert.Empty(body.RootElement.GetProperty("active").EnumerateArray());
        var course = Assert.Single(body.RootElement.GetProperty("ended").EnumerateArray());
        Assert.Equal("Current published title", course.GetProperty("title").GetString());
        Assert.Equal("2026-10-01", course.GetProperty("endedOn").GetString());
        Assert.Equal("grant-ended", course.GetProperty("endedReason").GetString());
        Assert.Equal(50, course.GetProperty("progress").GetProperty("percent").GetInt32());
        Assert.Equal(5, course.GetProperty("progress").GetProperty("completedLessons").GetInt32());
        Assert.Equal(10, course.GetProperty("progress").GetProperty("totalLessons").GetInt32());
    }

    [Fact(DisplayName = nameof(NewCourtesyReturnsCourseToActiveWithoutLosingProgress))]
    public async Task NewCourtesyReturnsCourseToActiveWithoutLosingProgress()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync(10, 5);
        factory.Commerce.CourseAccess = new([Ended(fixture)]);
        using var ended = await GetAsync(fixture);
        var previous = ended.RootElement.GetProperty("ended")[0].GetProperty("progress").GetRawText();
        factory.Commerce.CourseAccess = new([new(fixture.Course.Id, "active", Now, null, null, null)]);
        using var active = await GetAsync(fixture);
        Assert.Empty(active.RootElement.GetProperty("ended").EnumerateArray());
        var course = Assert.Single(active.RootElement.GetProperty("active").EnumerateArray());
        Assert.Equal(fixture.Course.Id, course.GetProperty("courseId").GetGuid());
        Assert.Equal(previous, course.GetProperty("progress").GetRawText());
        Assert.True(course.GetProperty("started").GetBoolean());
    }

    [Fact(DisplayName = nameof(CommerceWithoutResponseReturns503InsteadOfEmptyCourses))]
    public async Task CommerceWithoutResponseReturns503InsteadOfEmptyCourses()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync();
        factory.Commerce.Timeout = true;
        try
        {
            using var client = fixture.Client();
            using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
            Assert.Equal("COURSE_ACCESS_UNAVAILABLE", body.RootElement.GetProperty("code").GetString());
            Assert.False(body.RootElement.TryGetProperty("active", out _));
            Assert.False(body.RootElement.TryGetProperty("ended", out _));
        }
        finally { factory.Commerce.Timeout = false; }
    }

    [Theory(DisplayName = nameof(ProgressReadFailureKeepsCoursesAndFallsBackToFirstLessons))]
    [InlineData("timeout")]
    [InlineData("database")]
    [InlineData("connection")]
    public async Task ProgressReadFailureKeepsCoursesAndFallsBackToFirstLessons(string failure)
    {
        var active = new CourseProgressReadTestFixture(factory); await active.SeedAsync(10, 5);
        var ended = new CourseProgressReadTestFixture(factory); await ended.SeedAsync(tenantId: active.Course.TenantId);
        factory.Commerce.CourseAccess = new([new(active.Course.Id, "active", Now, null, null, null), Ended(ended)]);
        factory.CourseReads.Failure = failure switch
        {
            "database" => new NpgsqlException("Controlled progress database failure"),
            "connection" => new NpgsqlException("Controlled progress connection failure", new IOException("Controlled connection failure")),
            _ => new TimeoutException("Controlled progress timeout")
        };
        try
        {
            using var body = await GetAsync(active);
            Assert.False(body.RootElement.GetProperty("progressAvailable").GetBoolean());
            var course = Assert.Single(body.RootElement.GetProperty("active").EnumerateArray());
            Assert.Equal(active.Course.Title, course.GetProperty("title").GetString());
            Assert.Equal(JsonValueKind.Null, course.GetProperty("progress").ValueKind);
            Assert.Equal(JsonValueKind.Null, course.GetProperty("started").ValueKind);
            Assert.Equal(JsonValueKind.Null, course.GetProperty("lastActivityAt").ValueKind);
            Assert.Equal(active.Lessons[0].LessonId, course.GetProperty("continueLessonId").GetGuid());
            var endedCourse = Assert.Single(body.RootElement.GetProperty("ended").EnumerateArray());
            Assert.Equal("2026-10-01", endedCourse.GetProperty("endedOn").GetString());
            Assert.Equal(JsonValueKind.Null, endedCourse.GetProperty("progress").ValueKind);
        }
        finally { factory.CourseReads.Failure = null; }
    }

    [Fact(DisplayName = nameof(EndedCoursesAreOrderedByActualEndDescending))]
    public async Task EndedCoursesAreOrderedByActualEndDescending()
    {
        var older = new CourseProgressReadTestFixture(factory); await older.SeedAsync();
        var newer = new CourseProgressReadTestFixture(factory); await newer.SeedAsync(tenantId: older.Course.TenantId);
        factory.Commerce.CourseAccess = new([Ended(older, Now.AddDays(-2)), Ended(newer)]);
        using var body = await GetAsync(older);
        Assert.Equal([newer.Course.Id, older.Course.Id], body.RootElement.GetProperty("ended").EnumerateArray()
            .Select(course => course.GetProperty("courseId").GetGuid()));
    }

    [Fact(DisplayName = nameof(CurrentVersionReadFailureDoesNotBecomeProgressDegradation))]
    public async Task CurrentVersionReadFailureDoesNotBecomeProgressDegradation()
    {
        var fixture = new CourseProgressReadTestFixture(factory); await fixture.SeedAsync();
        factory.Commerce.CourseAccess = new([Ended(fixture)]);
        factory.CourseReads.Table = "content.course_versions";
        factory.CourseReads.Failure = new TimeoutException("Controlled current version timeout");
        try
        {
            using var client = fixture.Client();
            using var response = await client.GetAsync("/internal/v1/student-courses", Cancellation);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
            Assert.False(body.RootElement.TryGetProperty("progressAvailable", out _));
        }
        finally { factory.CourseReads.Failure = null; factory.CourseReads.Table = "progress.lesson_progress"; }
    }
}
