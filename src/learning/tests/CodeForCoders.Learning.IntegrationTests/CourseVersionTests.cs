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
public sealed class CourseVersionTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);

    [Fact(DisplayName = nameof(RepublishingPreservesSnapshotAndLessonIdentityAndHasOnlyOneCurrentVersion))]
    public async Task RepublishingPreservesSnapshotAndLessonIdentityAndHasOnlyOneCurrentVersion()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var first = await WriteAsync(client, course, "versions", new { draftRevision = 1, versionNote = "First note" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var v1 = await ReadAsync(client, course, "/versions/1");
        using var edit = await WriteAsync(client, course, "", new { title = "New draft" }, HttpMethod.Patch);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using var remove = await WriteAsync(client, course, $"lessons/{course.Modules[0].Lessons[1].Id}", null, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        using var second = await WriteAsync(client, course, "versions", new { draftRevision = 3, versionNote = "Second note" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var historical = await ReadAsync(client, course, "/versions/1");
        Assert.Equal(v1.GetProperty("modules").GetRawText(), historical.GetProperty("modules").GetRawText());
        Assert.Equal(course.Title, historical.GetProperty("title").GetString()); Assert.False(historical.GetProperty("current").GetBoolean());
        var current = await ReadAsync(client, course, "/versions/2"); Assert.True(current.GetProperty("current").GetBoolean());
        Assert.Equal("New draft", current.GetProperty("title").GetString());
        var lesson = Assert.Single(current.GetProperty("modules")[0].GetProperty("lessons").EnumerateArray());
        Assert.Equal(course.Modules[0].Lessons[0].Id, lesson.GetProperty("lessonId").GetGuid());
        var history = await ReadAsync(client, course, "/versions"); Assert.Equal(2, history.GetProperty("data")[0].GetProperty("versionNumber").GetInt32());
        Assert.Equal("Teacher display name", history.GetProperty("data")[0].GetProperty("publishedBy").GetProperty("name").GetString());
        Assert.Equal("Second note", history.GetProperty("data")[0].GetProperty("versionNote").GetString());
        Assert.True(history.GetProperty("data")[0].TryGetProperty("publishedAt", out _));
        await using var context = Context(); Assert.Equal(4, await context.ContentOutboxMessages.IgnoreQueryFilters().CountAsync(row => row.TenantId == course.TenantId, Cancellation));
    }

    [Fact(DisplayName = nameof(EditingThenRevertingContentClearsIndicatorDespiteHigherRevision))]
    public async Task EditingThenRevertingContentClearsIndicatorDespiteHigherRevision()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        using var edit = await WriteAsync(client, course, "", new { title = "Changed" }, HttpMethod.Patch);
        Assert.True((await edit.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
        using var revert = await WriteAsync(client, course, "", new { title = course.Title }, HttpMethod.Patch);
        var draft = await revert.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.False(draft.GetProperty("hasUnpublishedChanges").GetBoolean()); Assert.Equal(3, draft.GetProperty("draftRevision").GetInt32());
        var page = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses", Cancellation);
        Assert.False(page.GetProperty("data")[0].GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    [Fact(DisplayName = nameof(DiscardRestoresRemovedModuleLessonsAndAllSnapshotFieldsAndIds))]
    public async Task DiscardRestoresRemovedModuleLessonsAndAllSnapshotFieldsAndIds()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        using var remove = await WriteAsync(client, course, $"modules/{course.Modules[0].Id}", null, HttpMethod.Delete);
        using var edit = await WriteAsync(client, course, "", new { title = "Changed", description = "New description" }, HttpMethod.Patch);
        using var discard = await WriteAsync(client, course, "discard-draft", new { draftRevision = 3 }); Assert.Equal(HttpStatusCode.OK, discard.StatusCode);
        var restored = await discard.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(course.Title, restored.GetProperty("title").GetString()); Assert.Equal(course.Description, restored.GetProperty("description").GetString());
        Assert.Equal(4, restored.GetProperty("draftRevision").GetInt32()); Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean());
        var module = restored.GetProperty("modules")[0]; Assert.Equal(course.Modules[0].Id, module.GetProperty("moduleId").GetGuid());
        Assert.Equal(course.Modules[0].Lessons.Select(lesson => lesson.Id), module.GetProperty("lessons").EnumerateArray().Select(lesson => lesson.GetProperty("lessonId").GetGuid()));
        Assert.Equal(course.Modules[0].Lessons[0].Description, module.GetProperty("lessons")[0].GetProperty("description").GetString());
        Assert.Equal(course.Modules[0].Lessons[0].VideoId, module.GetProperty("lessons")[0].GetProperty("video").GetProperty("videoId").GetGuid());
        Assert.Equal(restored.GetProperty("modules").GetRawText(), (await ReadAsync(client, course)).GetProperty("modules").GetRawText());
    }

    [Fact(DisplayName = nameof(DiscardRestoresMovedExistingLessonWithoutDuplicateTrackingOrLosingIdentity))]
    public async Task DiscardRestoresMovedExistingLessonWithoutDuplicateTrackingOrLosingIdentity()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        using var move = await WriteAsync(client, course, $"lessons/{course.Modules[0].Lessons[0].Id}", new { moduleId = course.Modules[1].Id, title = "Changed lesson", description = "Changed description" }, HttpMethod.Patch);
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        using var discard = await WriteAsync(client, course, "discard-draft", new { draftRevision = 2 }); Assert.Equal(HttpStatusCode.OK, discard.StatusCode);
        var read = await ReadAsync(client, course); var lesson = read.GetProperty("modules")[0].GetProperty("lessons")[0];
        Assert.Equal(course.Modules[0].Lessons[0].Id, lesson.GetProperty("lessonId").GetGuid()); Assert.Equal("Lesson 1", lesson.GetProperty("title").GetString());
        Assert.Equal(2, read.GetProperty("modules")[0].GetProperty("lessons").GetArrayLength()); Assert.Single(read.GetProperty("modules")[1].GetProperty("lessons").EnumerateArray());
    }

    [Fact(DisplayName = nameof(StaleDiscardAndPublicationDoNotChangeTheNewerDraft))]
    public async Task StaleDiscardAndPublicationDoNotChangeTheNewerDraft()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        using var edit = await WriteAsync(client, course, "", new { title = "Colleague edit" }, HttpMethod.Patch);
        foreach (var operation in new[] { "discard-draft", "versions" })
        {
            using var response = await WriteAsync(client, course, operation, new { draftRevision = 1 });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("DRAFT_CHANGED", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        }
        Assert.Equal("Colleague edit", (await ReadAsync(client, course)).GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(NeverPublishedRejectsDiscardAndReturnsEmptyHistory))]
    public async Task NeverPublishedRejectsDiscardAndReturnsEmptyHistory()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var discard = await WriteAsync(client, course, "discard-draft", new { draftRevision = 1 }); Assert.Equal(HttpStatusCode.Conflict, discard.StatusCode);
        Assert.Equal("COURSE_NEVER_PUBLISHED", (await discard.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        Assert.Empty((await ReadAsync(client, course, "/versions")).GetProperty("data").EnumerateArray());
    }

    [Fact(DisplayName = nameof(ConcurrentDistinctPublicationIntentsProduceSequentialVersions))]
    public async Task ConcurrentDistinctPublicationIntentsProduceSequentialVersions()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        var responses = await Task.WhenAll(WriteAsync(client, course, "versions", new { draftRevision = 1 }), WriteAsync(client, course, "versions", new { draftRevision = 1 }));
        var numbers = new List<int>();
        foreach (var response in responses) { using (response) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); numbers.Add((await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("versionNumber").GetInt32()); } }
        Assert.Equal(new[] { 2, 3 }, numbers.Order());
        Assert.Equal(3, (await ReadAsync(client, course)).GetProperty("currentVersion").GetInt32());
    }

    [Fact(DisplayName = nameof(RepeatedRepublicationIntentDoesNotCreateExtraVersion))]
    public async Task RepeatedRepublicationIntentDoesNotCreateExtraVersion()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course); var key = Guid.CreateVersion7().ToString();
        using var first = await WriteAsync(client, course, "versions", new { draftRevision = 1, versionNote = "Second" }, key: key);
        using var replay = await WriteAsync(client, course, "versions", new { draftRevision = 1, versionNote = "Second" }, key: key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, (await ReadAsync(client, course, "/versions")).GetProperty("data").GetArrayLength());
    }

    [Fact(DisplayName = nameof(DiscardRetryIsIdempotentAndChangedBodyIsRejected))]
    public async Task DiscardRetryIsIdempotentAndChangedBodyIsRejected()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course); var key = Guid.CreateVersion7().ToString();
        using var first = await WriteAsync(client, course, "discard-draft", new { draftRevision = 1 }, key: key);
        using var replay = await WriteAsync(client, course, "discard-draft", new { draftRevision = 1 }, key: key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        using var changed = await WriteAsync(client, course, "discard-draft", new { draftRevision = 2 }, key: key); Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        Assert.Equal(2, (await ReadAsync(client, course)).GetProperty("draftRevision").GetInt32());
    }

    [Fact(DisplayName = nameof(HistoryPaginationAndMissingVersionRespectContract))]
    public async Task HistoryPaginationAndMissingVersionRespectContract()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course); await PublishAsync(client, course);
        var page = await ReadAsync(client, course, "/versions?_page=2&_size=1"); Assert.Equal(1, page.GetProperty("data")[0].GetProperty("versionNumber").GetInt32());
        Assert.Equal(2, page.GetProperty("pagination").GetProperty("total").GetInt32());
        using var invalid = await client.GetAsync($"/internal/v1/courses/{course.Id}/versions?_page=0", Cancellation); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await client.GetAsync($"/internal/v1/courses/{course.Id}/versions/9", Cancellation); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("VERSION_NOT_FOUND", (await missing.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(OtherTenantCannotReadVersionsOrDiscardAndReaderCannotDiscard))]
    public async Task OtherTenantCannotReadVersionsOrDiscardAndReaderCannotDiscard()
    {
        var course = await SeedAsync(); using var teacher = Teacher(course.TenantId); await PublishAsync(teacher, course);
        using var other = Teacher(Guid.CreateVersion7());
        foreach (var path in new[] { "/versions", "/versions/1" }) { using var response = await other.GetAsync($"/internal/v1/courses/{course.Id}{path}", Cancellation); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        using var discard = await WriteAsync(other, course, "discard-draft", new { draftRevision = 1 }); Assert.Equal(HttpStatusCode.NotFound, discard.StatusCode);
        using var reader = factory.Actor(course.TenantId, Guid.CreateVersion7(), ["autoria.ler"]);
        Assert.Equal(1, (await ReadAsync(reader, course, "/versions/1")).GetProperty("versionNumber").GetInt32());
        using var forbidden = await WriteAsync(reader, course, "discard-draft", new { draftRevision = 1 }); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact(DisplayName = nameof(DiscardRestoresLatestVersionRatherThanTheFirstPublication))]
    public async Task DiscardRestoresLatestVersionRatherThanTheFirstPublication()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course);
        using var edit = await WriteAsync(client, course, "", new { title = "Version two" }, HttpMethod.Patch);
        using var second = await WriteAsync(client, course, "versions", new { draftRevision = 2 }); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var newEdit = await WriteAsync(client, course, "", new { title = "Unpublished third" }, HttpMethod.Patch);
        using var discard = await WriteAsync(client, course, "discard-draft", new { draftRevision = 3 }); Assert.Equal(HttpStatusCode.OK, discard.StatusCode);
        var restored = await ReadAsync(client, course); Assert.Equal("Version two", restored.GetProperty("title").GetString());
        Assert.Equal(2, restored.GetProperty("currentVersion").GetInt32()); Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean());
        Assert.Equal(course.Title, (await ReadAsync(client, course, "/versions/1")).GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(RevertingLessonFieldsAndOrderClearsContentIndicator))]
    public async Task RevertingLessonFieldsAndOrderClearsContentIndicator()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); await PublishAsync(client, course); var lesson = course.Modules[0].Lessons[0];
        using var edit = await WriteAsync(client, course, $"lessons/{lesson.Id}", new { title = "Changed", description = "Changed", position = 2 }, HttpMethod.Patch);
        Assert.True((await ReadAsync(client, course)).GetProperty("hasUnpublishedChanges").GetBoolean());
        using var revert = await WriteAsync(client, course, $"lessons/{lesson.Id}", new { title = lesson.Title, description = lesson.Description, position = 1 }, HttpMethod.Patch);
        Assert.Equal(HttpStatusCode.OK, revert.StatusCode); Assert.False((await ReadAsync(client, course)).GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    private async Task<Course> SeedAsync()
    {
        var course = Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Creator", "Version course", "Description", DateTimeOffset.UtcNow));
        var module = course.AddModule(Changes("First")); course.AddLesson(module, Changes("Lesson 1")); course.AddLesson(module, Changes("Lesson 2"));
        module = course.AddModule(Changes("Second")); course.AddLesson(module, Changes("Lesson 3"));
        await using var context = Context(); context.Courses.Add(course); await context.SaveChangesAsync(Cancellation); return course;
    }

    private static CourseChanges Changes(string title) => new(title, "Lesson description", true, null, null, Guid.CreateVersion7(), true);
    private static async Task PublishAsync(HttpClient client, Course course)
    { using var response = await WriteAsync(client, course, "versions", new { draftRevision = 1 }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); }
    private static Task<JsonElement> ReadAsync(HttpClient client, Course course, string suffix = "") => client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course.Id}{suffix}", Cancellation);
    private static async Task<HttpResponseMessage> WriteAsync(HttpClient client, Course course, string operation, object? body, HttpMethod? method = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, $"/internal/v1/courses/{course.Id}{(operation.Length == 0 ? "" : "/" + operation)}") { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); return await client.SendAsync(request, Cancellation);
    }
}
