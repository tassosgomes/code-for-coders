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
public sealed class CoursePrerequisiteTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());

    [Fact(DisplayName = nameof(TextAndOrderedRecommendationsPersistAndReadCurrentDraftTitles))]
    public async Task TextAndOrderedRecommendationsPersistAndReadCurrentDraftTitles()
    {
        var target = await SeedAsync(); var first = await SeedAsync(target.TenantId, "First", true); var second = await SeedAsync(target.TenantId, "Second", true);
        using var client = Teacher(target.TenantId);
        using var response = await WriteAsync(client, target.Id, new { prerequisiteText = new string('x', 1000), recommendedCourseIds = new[] { second.Id, first.Id } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var output = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(second.Id, output.GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("courseId").GetGuid());
        using var rename = await WriteAsync(client, first.Id, new { title = "Current draft title" }); rename.EnsureSuccessStatusCode();
        var read = await ReadAsync(client, target.Id);
        Assert.Equal(1000, read.GetProperty("prerequisite").GetProperty("text").GetString()!.Length);
        Assert.Equal("Current draft title", read.GetProperty("prerequisite").GetProperty("recommendedCourses")[1].GetProperty("title").GetString());
        await using var db = Context(); var persisted = await db.Courses.IgnoreQueryFilters().SingleAsync(course => course.Id == target.Id, Cancellation);
        Assert.Equal([second.Id, first.Id], persisted.RecommendedCourseIds);
    }

    [Fact(DisplayName = nameof(AbsentFieldsPreserveAndNullTextAndEmptyListClearIndependently))]
    public async Task AbsentFieldsPreserveAndNullTextAndEmptyListClearIndependently()
    {
        var target = await SeedAsync(); var recommended = await SeedAsync(target.TenantId, published: true); using var client = Teacher(target.TenantId);
        using var set = await WriteAsync(client, target.Id, new { prerequisiteText = "Basics", recommendedCourseIds = new[] { recommended.Id } }); set.EnsureSuccessStatusCode();
        using var rename = await WriteAsync(client, target.Id, new { title = "Renamed" }); rename.EnsureSuccessStatusCode();
        Assert.Equal("Basics", (await ReadAsync(client, target.Id)).GetProperty("prerequisite").GetProperty("text").GetString());
        using var clearText = await WriteAsync(client, target.Id, new { prerequisiteText = (string?)null }); clearText.EnsureSuccessStatusCode();
        Assert.Equal(1, (await ReadAsync(client, target.Id)).GetProperty("prerequisite").GetProperty("recommendedCourses").GetArrayLength());
        using var clearList = await WriteAsync(client, target.Id, new { recommendedCourseIds = Array.Empty<Guid>() }); clearList.EnsureSuccessStatusCode();
        var read = (await ReadAsync(client, target.Id)).GetProperty("prerequisite");
        Assert.Equal(JsonValueKind.Null, read.GetProperty("text").ValueKind); Assert.Equal(0, read.GetProperty("recommendedCourses").GetArrayLength());
    }

    [Theory(DisplayName = nameof(InvalidRecommendedCourseRejectsWholeRequest))]
    [InlineData("self")]
    [InlineData("unpublished")]
    [InlineData("other-tenant")]
    [InlineData("missing")]
    public async Task InvalidRecommendedCourseRejectsWholeRequest(string kind)
    {
        var target = await SeedAsync(); var valid = await SeedAsync(target.TenantId, published: true);
        var invalid = kind switch
        {
            "self" => target.Id,
            "unpublished" => (await SeedAsync(target.TenantId)).Id,
            "other-tenant" => (await SeedAsync(published: true)).Id,
            _ => Guid.CreateVersion7()
        };
        using var client = Teacher(target.TenantId); var before = await ReadAsync(client, target.Id);
        using var response = await WriteAsync(client, target.Id, new { level = "advanced", title = "Rejected", prerequisiteText = "Do not persist", recommendedCourseIds = new[] { valid.Id, invalid } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("RECOMMENDED_COURSE_INVALID", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("recommendedCourseIds[1]", out _));
        Assert.DoesNotContain("Do not persist", problem.GetRawText());
        Assert.Equal(before.GetRawText(), (await ReadAsync(client, target.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(OtherTenantAndMissingRecommendationReturnIndistinguishableProblems))]
    public async Task OtherTenantAndMissingRecommendationReturnIndistinguishableProblems()
    {
        var target = await SeedAsync(); var foreign = await SeedAsync(published: true); using var client = Teacher(target.TenantId);
        using var other = await WriteAsync(client, target.Id, new { recommendedCourseIds = new[] { foreign.Id } });
        using var missing = await WriteAsync(client, target.Id, new { recommendedCourseIds = new[] { Guid.CreateVersion7() } });
        var first = await other.Content.ReadFromJsonAsync<JsonElement>(Cancellation); var second = await missing.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(other.StatusCode, missing.StatusCode);
        foreach (var field in new[] { "type", "title", "status", "code", "detail", "errors" }) Assert.Equal(first.GetProperty(field).GetRawText(), second.GetProperty(field).GetRawText());
    }

    [Fact(DisplayName = nameof(SchemaViolationsReturn400AndDoNotAlterDraft))]
    public async Task SchemaViolationsReturn400AndDoNotAlterDraft()
    {
        var target = await SeedAsync(); using var client = Teacher(target.TenantId); var before = await ReadAsync(client, target.Id); var id = Guid.CreateVersion7();
        var invalidLists = new object?[] { new[] { id, id }, Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7()).ToArray(), new[] { "not-a-uuid" }, new[] { 42 }, null, "invalid" };
        foreach (var recommendedCourseIds in invalidLists)
        {
            using var response = await WriteAsync(client, target.Id, new { title = "Rejected", recommendedCourseIds });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("INVALID_REQUEST", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        }
        Assert.Equal(before.GetRawText(), (await ReadAsync(client, target.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(TooLongOrEmptyTextReturnsFieldErrorAndPreservesDraft))]
    public async Task TooLongOrEmptyTextReturnsFieldErrorAndPreservesDraft()
    {
        var target = await SeedAsync(); using var client = Teacher(target.TenantId); var before = await ReadAsync(client, target.Id);
        foreach (var text in new[] { new string('x', 1001), "" })
        {
            using var response = await WriteAsync(client, target.Id, new { prerequisiteText = text, level = "beginner" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.Equal("FIELD_INVALID", problem.GetProperty("code").GetString()); Assert.Contains("prerequisiteText", problem.GetProperty("detail").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("prerequisiteText", out _));
            if (text.Length > 0) Assert.DoesNotContain(text, problem.GetRawText());
        }
        Assert.Equal(before.GetRawText(), (await ReadAsync(client, target.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(MutualRecommendationsAreAccepted))]
    public async Task MutualRecommendationsAreAccepted()
    {
        var first = await SeedAsync(published: true); var second = await SeedAsync(first.TenantId, published: true); using var client = Teacher(first.TenantId);
        using var forward = await WriteAsync(client, first.Id, new { recommendedCourseIds = new[] { second.Id } }); forward.EnsureSuccessStatusCode();
        using var backward = await WriteAsync(client, second.Id, new { recommendedCourseIds = new[] { first.Id } }); backward.EnsureSuccessStatusCode();
    }

    [Fact(DisplayName = nameof(ReorderingOnlyChangesRevisionAndFingerprintAndDiscardRestoresPublishedPrerequisite))]
    public async Task ReorderingOnlyChangesRevisionAndFingerprintAndDiscardRestoresPublishedPrerequisite()
    {
        var target = await SeedAsync(published: true); var first = await SeedAsync(target.TenantId, published: true); var second = await SeedAsync(target.TenantId, published: true);
        await using (var db = Context())
        {
            var course = await db.Courses.IgnoreQueryFilters().Include(item => item.Modules).ThenInclude(item => item.Lessons).SingleAsync(item => item.Id == target.Id, Cancellation);
            course.Update(new(null, null, false, null, null, PrerequisiteText: "Basics", HasPrerequisiteText: true, RecommendedCourseIds: [first.Id, second.Id]));
            db.CourseVersions.Add(course.Publish(new(1, null, new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow), [new(first.Id, first.Title), new(second.Id, second.Title)])));
            await db.SaveChangesAsync(Cancellation);
        }
        using var client = Teacher(target.TenantId); var snapshot = await client.GetStringAsync($"/internal/v1/courses/{target.Id}/versions/2", Cancellation);
        using var reordered = await WriteAsync(client, target.Id, new { recommendedCourseIds = new[] { second.Id, first.Id } });
        var changed = await reordered.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(changed.GetProperty("hasUnpublishedChanges").GetBoolean()); Assert.Equal(2, changed.GetProperty("draftRevision").GetInt32());
        using var revert = await WriteAsync(client, target.Id, new { recommendedCourseIds = new[] { first.Id, second.Id } });
        Assert.False((await revert.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
        using var discard = await WriteAsync(client, target.Id, new { draftRevision = 3 }, suffix: "/discard-draft"); discard.EnsureSuccessStatusCode();
        var restored = await discard.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("Basics", restored.GetProperty("prerequisite").GetProperty("text").GetString());
        Assert.Equal(2, restored.GetProperty("prerequisite").GetProperty("recommendedCourses").GetArrayLength()); Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean());
        Assert.Equal(snapshot, await client.GetStringAsync($"/internal/v1/courses/{target.Id}/versions/2", Cancellation));
    }

    [Fact(DisplayName = nameof(TextOnlyEditChangesFingerprintAndNullRestoresIt))]
    public async Task TextOnlyEditChangesFingerprintAndNullRestoresIt()
    {
        var target = await SeedAsync(published: true); using var client = Teacher(target.TenantId);
        using var set = await WriteAsync(client, target.Id, new { prerequisiteText = "Basics" });
        Assert.True((await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
        using var clear = await WriteAsync(client, target.Id, new { prerequisiteText = (string?)null });
        Assert.False((await clear.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    [Fact(DisplayName = nameof(IdempotentReplayReturnsOriginalResponseWithoutSecondEdit))]
    public async Task IdempotentReplayReturnsOriginalResponseWithoutSecondEdit()
    {
        var target = await SeedAsync(); var recommended = await SeedAsync(target.TenantId, published: true); using var client = Teacher(target.TenantId); var key = Guid.CreateVersion7().ToString();
        var body = new { prerequisiteText = "Basics", recommendedCourseIds = new[] { recommended.Id } };
        using var first = await WriteAsync(client, target.Id, body, key); using var replay = await WriteAsync(client, target.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, (await ReadAsync(client, target.Id)).GetProperty("draftRevision").GetInt32());
        using var reused = await WriteAsync(client, target.Id, new { prerequisiteText = "Different" }, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact(DisplayName = nameof(SearchCombinesStatusNormalizesAccentsAndCannotExposeAnotherTenant))]
    public async Task SearchCombinesStatusNormalizesAccentsAndCannotExposeAnotherTenant()
    {
        var target = await SeedAsync(title: "Fundamentos de C#", published: true); await SeedAsync(target.TenantId, "Fundaméntos draft"); await SeedAsync(title: "Fundamentos foreign", published: true);
        using var client = Teacher(target.TenantId);
        foreach (var term in new[] { "fundaméntos", "FUNDAMENTOS" })
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses?status=published&title={Uri.EscapeDataString(term)}", Cancellation);
            Assert.Equal(1, page.GetProperty("data").GetArrayLength()); Assert.Equal(target.Id, page.GetProperty("data")[0].GetProperty("courseId").GetGuid());
        }
    }

    [Fact(DisplayName = nameof(LikeMetacharactersAndBackslashAreLiteral))]
    public async Task LikeMetacharactersAndBackslashAreLiteral()
    {
        var target = await SeedAsync(title: "100%_\\literal", published: true); await SeedAsync(target.TenantId, "100xx literal", true); using var client = Teacher(target.TenantId);
        foreach (var term in new[] { "0%", "%_", "_\\", "\\literal" })
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses?title={Uri.EscapeDataString(term)}", Cancellation);
            Assert.Equal(1, page.GetProperty("data").GetArrayLength()); Assert.Equal(target.Id, page.GetProperty("data")[0].GetProperty("courseId").GetGuid());
        }
    }

    [Fact(DisplayName = nameof(RenameAndDiscardMaintainTitleSearch))]
    public async Task RenameAndDiscardMaintainTitleSearch()
    {
        var target = await SeedAsync(title: "Fundaméntos", published: true); using var client = Teacher(target.TenantId);
        using var rename = await WriteAsync(client, target.Id, new { title = "Ações novas" }); rename.EnsureSuccessStatusCode();
        var page = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses?title=ACOES", Cancellation); Assert.Equal(1, page.GetProperty("data").GetArrayLength());
        using var discard = await WriteAsync(client, target.Id, new { draftRevision = 2 }, suffix: "/discard-draft"); discard.EnsureSuccessStatusCode();
        page = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses?title=fundamentos", Cancellation); Assert.Equal(1, page.GetProperty("data").GetArrayLength());
        page = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses?title=acoes", Cancellation); Assert.Equal(0, page.GetProperty("data").GetArrayLength());
    }

    [Fact(DisplayName = nameof(ReaderCannotChangePrerequisitesAndForeignTeacherCannotReachDraft))]
    public async Task ReaderCannotChangePrerequisitesAndForeignTeacherCannotReachDraft()
    {
        var target = await SeedAsync(); using var teacher = Teacher(target.TenantId); var before = await ReadAsync(teacher, target.Id);
        using var reader = factory.Actor(target.TenantId, Guid.CreateVersion7(), ["autoria.ler"]); using var foreign = Teacher(Guid.CreateVersion7());
        using var forbidden = await WriteAsync(reader, target.Id, new { prerequisiteText = "Basics" }); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var missing = await WriteAsync(foreign, target.Id, new { prerequisiteText = "Basics" }); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(before.GetRawText(), (await ReadAsync(teacher, target.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(MigrationBackfillsTitlesAndResetsFingerprintWithoutChangingIndicator))]
    public async Task MigrationBackfillsTitlesAndResetsFingerprintWithoutChangingIndicator()
    {
        await using var fixture = new CoursePrerequisiteLegacyFixture(); await fixture.InitializeAsync(Cancellation);
    }

    private async Task<Course> SeedAsync(Guid? tenant = null, string title = "Prerequisite course", bool published = false)
    {
        var course = Course.Create(new(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", title, null, DateTimeOffset.UtcNow));
        await using var context = Context(); context.Courses.Add(course);
        if (published)
        {
            var module = course.AddModule(new("Module", null, false, null, null)); course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
            context.CourseVersions.Add(course.Publish(new(1, null, new(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow))));
        }
        await context.SaveChangesAsync(Cancellation); return course;
    }

    private static Task<JsonElement> ReadAsync(HttpClient client, Guid course) => client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{course}", Cancellation);
    private static async Task<HttpResponseMessage> WriteAsync(HttpClient client, Guid course, object body, string? key = null, string suffix = "")
    {
        using var request = new HttpRequestMessage(suffix.Length == 0 ? HttpMethod.Patch : HttpMethod.Post, $"/internal/v1/courses/{course}{suffix}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); return await client.SendAsync(request, Cancellation);
    }
}
