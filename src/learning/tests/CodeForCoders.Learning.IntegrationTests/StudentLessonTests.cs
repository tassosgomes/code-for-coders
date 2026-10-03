using System.Net;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(StudentLessonCollection.Name)]
public sealed class StudentLessonTests(StudentLessonApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private LearningDbContext Context(Guid tenant) { var context = new TenantContext(); context.Set(tenant); return new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, context); }
    private async Task<Course> SeedAsync(bool published = true, Guid? tenant = null)
    {
        var actor = new CourseCreation(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Private course", null, DateTimeOffset.UtcNow);
        var course = Course.Create(actor); var module = course.AddModule(new("Private module", null, false, null, null));
        course.AddLesson(module, new("First lesson", null, false, null, null, Guid.CreateVersion7(), true));
        course.AddLesson(module, new("Second lesson", null, false, null, null, Guid.CreateVersion7(), true));
        await using var db = Context(course.TenantId); db.Courses.Add(course);
        if (published) db.CourseVersions.Add(course.Publish(new(course.DraftRevision, null, actor)));
        await db.SaveChangesAsync(Cancellation);
        factory.Commerce.Status = HttpStatusCode.OK; factory.Commerce.Timeout = false; factory.Commerce.PermittedCourseId = null;
        factory.Commerce.Decision = new("allowed", new("until", DateTimeOffset.UtcNow.AddMonths(6)), null, null, DateTimeOffset.UtcNow);
        return course;
    }
    private static Guid Lesson(Course course) => course.Modules[0].Lessons[0].Id;
    private static Task<HttpResponseMessage> Get(HttpClient client, Guid lesson) => client.GetAsync($"/internal/v1/lessons/{lesson}", Cancellation);
    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); var body = await response.Content.ReadAsStringAsync(Cancellation);
        using var json = JsonDocument.Parse(body); Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Private", body); Assert.DoesNotContain("First lesson", body); Assert.DoesNotContain("Second lesson", body);
    }

    [Fact(DisplayName = nameof(AllowedStudentReceivesOrderedCurriculumWithoutVideoIds))]
    public async Task AllowedStudentReceivesOrderedCurriculumWithoutVideoIds()
    {
        var course = await SeedAsync(); var student = Guid.CreateVersion7(); using var client = factory.Student(course.TenantId, student);
        using var response = await Get(client, Lesson(course)); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Cancellation); using var json = JsonDocument.Parse(text);
        Assert.Equal(Lesson(course), json.RootElement.GetProperty("lesson").GetProperty("lessonId").GetGuid());
        Assert.Equal(2, json.RootElement.GetProperty("course").GetProperty("modules")[0].GetProperty("lessons").GetArrayLength());
        Assert.DoesNotContain("videoId", text); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains($"studentId={student:D}", factory.Commerce.Path!); Assert.Contains($"courseId={course.Id:D}", factory.Commerce.Path!);
        new JwtSecurityTokenHandler().ValidateToken(factory.Commerce.Assertion!, new TokenValidationParameters { ValidIssuer = "learning", ValidAudience = "commerce", IssuerSigningKey = factory.AssertionPublicKey }, out _);
        var assertion = new JwtSecurityTokenHandler().ReadJwtToken(factory.Commerce.Assertion);
        Assert.Equal("access-decision:read", assertion.Claims.Single(c => c.Type == "scope").Value);
        Assert.Equal(course.TenantId.ToString(), assertion.Claims.Single(c => c.Type == "tenantId").Value);
    }

    [Theory(DisplayName = nameof(DeniedStudentSeesReasonAndNoCurriculum))]
    [InlineData("no-grant")]
    [InlineData("grant-ended")]
    public async Task DeniedStudentSeesReasonAndNoCurriculum(string reason)
    {
        var course = await SeedAsync(); var end = DateTimeOffset.UtcNow.AddDays(-1);
        factory.Commerce.Decision = new("denied", null, reason, reason == "grant-ended" ? end : null, DateTimeOffset.UtcNow);
        using var client = factory.Student(course.TenantId, Guid.CreateVersion7()); using var response = await Get(client, Lesson(course));
        await AssertError(response, HttpStatusCode.Forbidden, "ACCESS_DENIED");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)); Assert.Equal(reason, body.RootElement.GetProperty("reason").GetString());
        if (reason == "grant-ended") Assert.Equal(end, body.RootElement.GetProperty("accessEndedAt").GetDateTimeOffset());
    }

    [Theory(DisplayName = nameof(UnavailableDecisionIsNotCachedAndNextRequestRecovers))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableDecisionIsNotCachedAndNextRequestRecovers(bool timeout)
    {
        var course = await SeedAsync(); using var client = factory.Student(course.TenantId, Guid.CreateVersion7());
        factory.Commerce.Status = HttpStatusCode.InternalServerError; factory.Commerce.Timeout = timeout;
        using var first = await Get(client, Lesson(course)); await AssertError(first, HttpStatusCode.ServiceUnavailable, "ACCESS_DECISION_UNAVAILABLE");
        factory.Commerce.Status = HttpStatusCode.OK; factory.Commerce.Timeout = false; factory.Commerce.PermittedCourseId = null;
        using var second = await Get(client, Lesson(course)); Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Theory(DisplayName = nameof(UnavailableLessonsHaveOne404AndNeverConsultCommerce))]
    [InlineData("missing")]
    [InlineData("other-tenant")]
    [InlineData("draft")]
    [InlineData("removed")]
    public async Task UnavailableLessonsHaveOne404AndNeverConsultCommerce(string scenario)
    {
        var course = await SeedAsync(scenario != "draft"); var lesson = Lesson(course);
        if (scenario == "missing") lesson = Guid.CreateVersion7();
        if (scenario == "removed")
        {
            await using var db = Context(course.TenantId);
            var tracked = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync(c => c.Id == course.Id, Cancellation);
            tracked.RemoveLesson(lesson);
            db.CourseVersions.Add(tracked.Publish(new(tracked.DraftRevision, null, new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow))));
            await db.SaveChangesAsync(Cancellation);
        }
        var calls = factory.Commerce.Calls;
        using var client = factory.Student(scenario == "other-tenant" ? Guid.CreateVersion7() : course.TenantId, Guid.CreateVersion7());
        using var response = await Get(client, lesson); await AssertError(response, HttpStatusCode.NotFound, "LESSON_NOT_AVAILABLE"); Assert.Equal(calls, factory.Commerce.Calls);
    }

    [Theory(DisplayName = nameof(ActorAndInsufficientStudentScopeCannotOpenLesson))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActorAndInsufficientStudentScopeCannotOpenLesson(bool actor)
    {
        var course = await SeedAsync(); var calls = factory.Commerce.Calls;
        using var client = factory.Student(course.TenantId, Guid.CreateVersion7(), actor ? "lessons:read" : "unrelated:read", actor);
        using var response = await Get(client, Lesson(course)); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(calls, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(StudentCannotOpenAuthoring))]
    public async Task StudentCannotOpenAuthoring()
    {
        var course = await SeedAsync(); using var client = factory.Student(course.TenantId, Guid.CreateVersion7());
        using var response = await client.GetAsync($"/internal/v1/courses/{course.Id}", Cancellation); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = nameof(RepublishedLessonUsesNewestTitlesAndOrder))]
    public async Task RepublishedLessonUsesNewestTitlesAndOrder()
    {
        var course = await SeedAsync(); var lesson = Lesson(course);
        await using (var db = Context(course.TenantId))
        {
            var tracked = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync(c => c.Id == course.Id, Cancellation);
            tracked.UpdateLesson(lesson, new("New title", null, false, 2, null));
            db.CourseVersions.Add(tracked.Publish(new(tracked.DraftRevision, null, new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow))));
            await db.SaveChangesAsync(Cancellation);
        }
        using var client = factory.Student(course.TenantId, Guid.CreateVersion7()); using var response = await Get(client, lesson);
        response.EnsureSuccessStatusCode(); using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, body.RootElement.GetProperty("course").GetProperty("versionNumber").GetInt32());
        Assert.Equal("New title", body.RootElement.GetProperty("lesson").GetProperty("title").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("lesson").GetProperty("position").GetInt32());
    }
    [Fact(DisplayName = nameof(AccessToOneCourseDoesNotAuthorizeAnotherCourse))]
    public async Task AccessToOneCourseDoesNotAuthorizeAnotherCourse()
    {
        var first = await SeedAsync();
        var second = await SeedAsync(tenant: first.TenantId);
        factory.Commerce.PermittedCourseId = first.Id;
        using var client = factory.Student(first.TenantId, Guid.CreateVersion7());
        using var allowed = await Get(client, Lesson(first)); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var denied = await Get(client, Lesson(second)); await AssertError(denied, HttpStatusCode.Forbidden, "ACCESS_DENIED");
    }

    [Fact(DisplayName = nameof(QueryStudentIdCannotReplaceJwtSubject))]
    public async Task QueryStudentIdCannotReplaceJwtSubject()
    {
        var course = await SeedAsync(); var student = Guid.CreateVersion7();
        using var client = factory.Student(course.TenantId, student);
        using var response = await client.GetAsync($"/internal/v1/lessons/{Lesson(course)}?studentId={Guid.CreateVersion7()}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"studentId={student:D}", factory.Commerce.Path!);
    }

}
