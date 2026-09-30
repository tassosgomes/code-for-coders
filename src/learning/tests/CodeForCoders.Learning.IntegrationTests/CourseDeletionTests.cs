using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(CourseApiCollection.Name)]
public sealed class CourseDeletionTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);

    [Fact(DisplayName = nameof(DeletionRemovesDraftModulesLessonsAndListEntryWithoutOutbox))]
    public async Task DeletionRemovesDraftModulesLessonsAndListEntryWithoutOutbox()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var deleted = await DeleteAsync(client, course.Id, "delete-draft"); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsByteArrayAsync(Cancellation));
        using var missing = await client.GetAsync($"/internal/v1/courses/{course.Id}", Cancellation); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses", Cancellation); Assert.Empty(list.GetProperty("data").EnumerateArray());
        await using var context = Context();
        Assert.False(await context.Courses.IgnoreQueryFilters().AnyAsync(row => row.Id == course.Id, Cancellation));
        Assert.False(await context.Set<CourseModule>().AnyAsync(row => row.CourseId == course.Id, Cancellation));
        var moduleId = course.Modules[0].Id;
        Assert.False(await context.Set<CourseLesson>().AnyAsync(row => row.ModuleId == moduleId, Cancellation));
        Assert.False(await context.ContentOutboxMessages.IgnoreQueryFilters().AnyAsync(row => row.TenantId == course.TenantId, Cancellation));
        Assert.False(await context.OutboxMessages.IgnoreQueryFilters().AnyAsync(row => row.TenantId == course.TenantId, Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentRetryReplaysNoContentAfterAggregateIsGoneAndIsScopedToActorTenantAndCourse))]
    public async Task ConcurrentRetryReplaysNoContentAfterAggregateIsGoneAndIsScopedToActorTenantAndCourse()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        var responses = await Task.WhenAll(DeleteAsync(client, course.Id, "same-intent"), DeleteAsync(client, course.Id, "same-intent"));
        foreach (var response in responses) { using (response) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); }
        using var retry = await DeleteAsync(client, course.Id, "same-intent"); Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        using var otherKey = await DeleteAsync(client, course.Id, "new-intent"); Assert.Equal(HttpStatusCode.NotFound, otherKey.StatusCode);
        using var colleague = Teacher(course.TenantId); using var otherActor = await DeleteAsync(colleague, course.Id, "same-intent"); Assert.Equal(HttpStatusCode.NotFound, otherActor.StatusCode);
        using var outsider = Teacher(Guid.CreateVersion7()); using var otherTenant = await DeleteAsync(outsider, course.Id, "same-intent"); Assert.Equal(HttpStatusCode.NotFound, otherTenant.StatusCode);
        var second = await SeedAsync(course.TenantId); using var secondDeleted = await DeleteAsync(client, second.Id, "same-intent"); Assert.Equal(HttpStatusCode.NoContent, secondDeleted.StatusCode);
        await using var context = Context(); Assert.Equal(2, await context.CourseEditReceipts.IgnoreQueryFilters().CountAsync(row => row.TenantId == course.TenantId, Cancellation));
    }

    [Fact(DisplayName = nameof(PublishedCourseRejectsDeletionAndPreservesDraftVersionAndPublicationOutbox))]
    public async Task PublishedCourseRejectsDeletionAndPreservesDraftVersionAndPublicationOutbox()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/courses/{course.Id}/versions") { Content = JsonContent.Create(new { draftRevision = 1 }) };
        request.Headers.Add("Idempotency-Key", "publication");
        using var published = await client.SendAsync(request, Cancellation); Assert.Equal(HttpStatusCode.Created, published.StatusCode);
        var snapshot = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course.Id}/versions/1", Cancellation);
        var before = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course.Id}", Cancellation);
        using var deleted = await DeleteAsync(client, course.Id, "forbidden-delete"); Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("COURSE_ALREADY_PUBLISHED", (await deleted.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        var after = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course.Id}", Cancellation); Assert.Equal(before.GetRawText(), after.GetRawText());
        Assert.Equal(snapshot.GetRawText(), (await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course.Id}/versions/1", Cancellation)).GetRawText());
        await using var context = Context(); Assert.Equal(2, await context.ContentOutboxMessages.IgnoreQueryFilters().CountAsync(row => row.TenantId == course.TenantId, Cancellation));
        Assert.Equal(1, await context.CourseVersions.IgnoreQueryFilters().CountAsync(row => row.CourseId == course.Id, Cancellation));
    }

    [Fact(DisplayName = nameof(OtherTenantReceivesIndistinguishableNotFoundAndLeavesDraftIntact))]
    public async Task OtherTenantReceivesIndistinguishableNotFoundAndLeavesDraftIntact()
    {
        var course = await SeedAsync(); using var outsider = Teacher(Guid.CreateVersion7());
        using var hidden = await DeleteAsync(outsider, course.Id, "hidden"); using var absent = await DeleteAsync(outsider, Guid.CreateVersion7(), "absent");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Equal(absent.StatusCode, hidden.StatusCode);
        Assert.Equal((await absent.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString(), (await hidden.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        using var owner = Teacher(course.TenantId); using var detail = await owner.GetAsync($"/internal/v1/courses/{course.Id}", Cancellation); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        await using var context = Context(); Assert.False(await context.CourseEditReceipts.IgnoreQueryFilters().AnyAsync(row => row.TenantId == course.TenantId, Cancellation));
    }

    [Fact(DisplayName = nameof(ReaderAndMissingIntentCannotDeleteDraft))]
    public async Task ReaderAndMissingIntentCannotDeleteDraft()
    {
        var course = await SeedAsync(); using var reader = factory.Actor(course.TenantId, Guid.CreateVersion7(), ["autoria.ler"]);
        using var denied = await DeleteAsync(reader, course.Id, "reader"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var teacher = Teacher(course.TenantId); using var noKey = await DeleteAsync(teacher, course.Id, null); Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        using var detail = await teacher.GetAsync($"/internal/v1/courses/{course.Id}", Cancellation); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact(DisplayName = nameof(HistoricalPublicationBlocksDeletionEvenWithoutCurrentPointer))]
    public async Task HistoricalPublicationBlocksDeletionEvenWithoutCurrentPointer()
    {
        var course = await SeedAsync();
        await using (var context = Context())
        {
            var stored = await context.Courses.IgnoreQueryFilters().Include(row => row.Modules).ThenInclude(row => row.Lessons).SingleAsync(row => row.Id == course.Id, Cancellation);
            var version = stored.Publish(new(1, null, new(course.TenantId, Guid.CreateVersion7(), "Teacher", course.Title, null, DateTimeOffset.UtcNow)));
            context.CourseVersions.Add(version);
            context.Entry(stored).Property(row => row.CurrentVersion).CurrentValue = null;
            await context.SaveChangesAsync(Cancellation);
        }
        using var client = Teacher(course.TenantId); using var response = await DeleteAsync(client, course.Id, "historical");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("COURSE_ALREADY_PUBLISHED", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        await using var verification = Context(); Assert.True(await verification.Courses.IgnoreQueryFilters().AnyAsync(row => row.Id == course.Id, Cancellation));
        Assert.True(await verification.CourseVersions.IgnoreQueryFilters().AnyAsync(row => row.CourseId == course.Id, Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentPublicationAndDeletionCannotLeaveAnOrphanVersion))]
    public async Task ConcurrentPublicationAndDeletionCannotLeaveAnOrphanVersion()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/courses/{course.Id}/versions") { Content = JsonContent.Create(new { draftRevision = 1 }) };
        request.Headers.Add("Idempotency-Key", "racing-publication");
        var publication = client.SendAsync(request, Cancellation); var deletion = DeleteAsync(client, course.Id, "racing-deletion");
        using var published = await publication; using var deleted = await deletion;
        await using var context = Context();
        var versions = await context.CourseVersions.IgnoreQueryFilters().CountAsync(row => row.CourseId == course.Id, Cancellation);
        var outbox = await context.ContentOutboxMessages.IgnoreQueryFilters().CountAsync(row => row.TenantId == course.TenantId, Cancellation);
        if (published.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode); Assert.Equal(1, versions); Assert.Equal(2, outbox);
            Assert.True(await context.Courses.IgnoreQueryFilters().AnyAsync(row => row.Id == course.Id, Cancellation));
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, published.StatusCode); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Equal(0, versions); Assert.Equal(0, outbox); Assert.False(await context.Courses.IgnoreQueryFilters().AnyAsync(row => row.Id == course.Id, Cancellation));
        }
    }

    private async Task<Course> SeedAsync(Guid? tenant = null)
    {
        var course = Course.Create(new(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Creator", "Deletion course", "Description", DateTimeOffset.UtcNow));
        var module = course.AddModule(new("Module", null, false, null, null, null, false));
        course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
        await using var context = Context(); context.Courses.Add(course); await context.SaveChangesAsync(Cancellation); return course;
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid courseId, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/internal/v1/courses/{courseId}");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Cancellation);
    }
}
