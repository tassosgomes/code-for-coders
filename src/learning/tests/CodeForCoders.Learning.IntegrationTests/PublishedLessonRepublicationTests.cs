using System.Net;
using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(StudentLessonCollection.Name)]
public sealed class PublishedLessonRepublicationTests(StudentLessonApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private LearningDbContext Context(Guid tenant)
    {
        var context = new TenantContext();
        context.Set(tenant);
        return new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, context);
    }

    private async Task<Course> SeedAsync(bool published = true, Guid? tenant = null)
    {
        var actor = new CourseCreation(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Private course", null, DateTimeOffset.UtcNow);
        var course = Course.Create(actor);
        var module = course.AddModule(new("Private module", null, false, null, null));
        course.AddLesson(module, new("First lesson", null, false, null, null, Guid.CreateVersion7(), true));
        course.AddLesson(module, new("Second lesson", null, false, null, null, Guid.CreateVersion7(), true));
        await using var db = Context(course.TenantId);
        db.Courses.Add(course);
        if (published) db.CourseVersions.Add(course.Publish(new(course.DraftRevision, null, actor)));
        await db.SaveChangesAsync(Cancellation);
        factory.Commerce.Status = HttpStatusCode.OK;
        factory.Commerce.Timeout = false;
        factory.Commerce.PermittedCourseId = null;
        factory.Commerce.Decision = new("allowed", new("until", DateTimeOffset.UtcNow.AddMonths(6)), null, null, DateTimeOffset.UtcNow);
        return course;
    }

    private static Guid Lesson(Course course, int index = 0) => course.Modules[0].Lessons[index].Id;
    private static Task<HttpResponseMessage> Get(HttpClient client, Guid lesson) => client.GetAsync($"/internal/v1/lessons/{lesson}", Cancellation);

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("Private", body);
        Assert.DoesNotContain("First lesson", body);
        Assert.DoesNotContain("Second lesson", body);
    }

    [Fact(DisplayName = nameof(RepublishedLessonWithNewTitleAndPositionRetainsStableLessonId))]
    public async Task RepublishedLessonWithNewTitleAndPositionRetainsStableLessonId()
    {
        var course = await SeedAsync();
        var firstLessonId = Lesson(course, 0);
        var secondLessonId = Lesson(course, 1);

        await using (var db = Context(course.TenantId))
        {
            var tracked = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync(c => c.Id == course.Id, Cancellation);
            tracked.UpdateLesson(firstLessonId, new("Renamed first lesson", null, false, 2, null));
            tracked.UpdateLesson(secondLessonId, new("Renamed second lesson", null, false, 1, null));
            db.CourseVersions.Add(tracked.Publish(new(tracked.DraftRevision, "Reordered and renamed lessons", new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow))));
            await db.SaveChangesAsync(Cancellation);
        }

        using var client = factory.Student(course.TenantId, Guid.CreateVersion7());

        using var firstResponse = await Get(client, firstLessonId);
        firstResponse.EnsureSuccessStatusCode();
        using var firstBody = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, firstBody.RootElement.GetProperty("course").GetProperty("versionNumber").GetInt32());
        Assert.Equal(firstLessonId, firstBody.RootElement.GetProperty("lesson").GetProperty("lessonId").GetGuid());
        Assert.Equal("Renamed first lesson", firstBody.RootElement.GetProperty("lesson").GetProperty("title").GetString());
        Assert.Equal(2, firstBody.RootElement.GetProperty("lesson").GetProperty("position").GetInt32());

        var lessonsInModule = firstBody.RootElement.GetProperty("course").GetProperty("modules")[0].GetProperty("lessons");
        Assert.Equal(secondLessonId, lessonsInModule[0].GetProperty("lessonId").GetGuid());
        Assert.Equal("Renamed second lesson", lessonsInModule[0].GetProperty("title").GetString());
        Assert.Equal(1, lessonsInModule[0].GetProperty("position").GetInt32());
        Assert.Equal(firstLessonId, lessonsInModule[1].GetProperty("lessonId").GetGuid());
        Assert.Equal("Renamed first lesson", lessonsInModule[1].GetProperty("title").GetString());
        Assert.Equal(2, lessonsInModule[1].GetProperty("position").GetInt32());

        using var secondResponse = await Get(client, secondLessonId);
        secondResponse.EnsureSuccessStatusCode();
        using var secondBody = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(secondLessonId, secondBody.RootElement.GetProperty("lesson").GetProperty("lessonId").GetGuid());
        Assert.Equal(1, secondBody.RootElement.GetProperty("lesson").GetProperty("position").GetInt32());
    }

    [Fact(DisplayName = nameof(RemovedLessonFromRepublishedCourseReturns404LessonNotAvailableAndNeverConsultsCommerce))]
    public async Task RemovedLessonFromRepublishedCourseReturns404LessonNotAvailableAndNeverConsultsCommerce()
    {
        var course = await SeedAsync();
        var removedLessonId = Lesson(course, 1);

        await using (var db = Context(course.TenantId))
        {
            var tracked = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync(c => c.Id == course.Id, Cancellation);
            tracked.RemoveLesson(removedLessonId);
            db.CourseVersions.Add(tracked.Publish(new(tracked.DraftRevision, "Removed lesson 2", new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow))));
            await db.SaveChangesAsync(Cancellation);
        }

        var callsBefore = factory.Commerce.Calls;
        using var client = factory.Student(course.TenantId, Guid.CreateVersion7());

        using var response = await Get(client, removedLessonId);
        await AssertError(response, HttpStatusCode.NotFound, "LESSON_NOT_AVAILABLE");
        Assert.Equal(callsBefore, factory.Commerce.Calls);
    }

    [Fact(DisplayName = nameof(StudentWithAccessToCourseACannotAccessLessonOfCourseB))]
    public async Task StudentWithAccessToCourseACannotAccessLessonOfCourseB()
    {
        var courseA = await SeedAsync();
        var courseB = await SeedAsync(tenant: courseA.TenantId);

        factory.Commerce.PermittedCourseId = courseA.Id;
        using var client = factory.Student(courseA.TenantId, Guid.CreateVersion7());

        using var responseA = await Get(client, Lesson(courseA));
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);

        using var responseB = await Get(client, Lesson(courseB));
        await AssertError(responseB, HttpStatusCode.Forbidden, "ACCESS_DENIED");
    }

    [Fact(DisplayName = nameof(RepublishingCourseDoesNotRevokeOrAlterStudentCourseEntitlement))]
    public async Task RepublishingCourseDoesNotRevokeOrAlterStudentCourseEntitlement()
    {
        var course = await SeedAsync();
        var lessonId = Lesson(course);
        var studentId = Guid.CreateVersion7();

        using var client = factory.Student(course.TenantId, studentId);

        using var responseV1 = await Get(client, lessonId);
        Assert.Equal(HttpStatusCode.OK, responseV1.StatusCode);
        using var bodyV1 = JsonDocument.Parse(await responseV1.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(1, bodyV1.RootElement.GetProperty("course").GetProperty("versionNumber").GetInt32());

        await using (var db = Context(course.TenantId))
        {
            var tracked = await db.Courses.Include(c => c.Modules).ThenInclude(m => m.Lessons).SingleAsync(c => c.Id == course.Id, Cancellation);
            tracked.Update(new("Private course revised", null, false, null, null));
            db.CourseVersions.Add(tracked.Publish(new(tracked.DraftRevision, "Version 2 note", new(course.TenantId, course.CreatedById, "Teacher", "Private course revised", null, DateTimeOffset.UtcNow))));
            await db.SaveChangesAsync(Cancellation);
        }

        using var responseV2 = await Get(client, lessonId);
        Assert.Equal(HttpStatusCode.OK, responseV2.StatusCode);
        using var bodyV2 = JsonDocument.Parse(await responseV2.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, bodyV2.RootElement.GetProperty("course").GetProperty("versionNumber").GetInt32());
        Assert.Equal("Private course revised", bodyV2.RootElement.GetProperty("course").GetProperty("title").GetString());
    }
}
