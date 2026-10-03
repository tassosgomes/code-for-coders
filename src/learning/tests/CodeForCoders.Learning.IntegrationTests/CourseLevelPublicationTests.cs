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
public sealed class CourseLevelPublicationTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);

    [Theory(DisplayName = nameof(OutboxFactMatchesAsyncApiIncludingExplicitNullsAndOrderedReferences))]
    [InlineData("advanced", true)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task OutboxFactMatchesAsyncApiIncludingExplicitNullsAndOrderedReferences(string? level, bool prerequisites)
    {
        var course = await SeedAsync(description: prerequisites ? "Pedagogical description" : null);
        using var client = Teacher(course.TenantId);
        var first = await SeedAsync(course.TenantId, "First published title");
        var second = await SeedAsync(course.TenantId, "Second published title");
        await PublishAsync(client, first); await PublishAsync(client, second);
        await PatchAsync(client, course.Id, new
        {
            level,
            prerequisiteText = prerequisites ? "Basics" : null,
            recommendedCourseIds = prerequisites ? new[] { second.Id, first.Id } : []
        });
        var version = await PublishAsync(client, course);
        var detail = await ReadAsync(client, course.Id);
        Assert.Equal(level, detail.GetProperty("currentLevel").GetString()); Assert.False(detail.GetProperty("hasUnpublishedChanges").GetBoolean());
        await using var db = Context();
        var persisted = await db.CourseVersions.IgnoreQueryFilters().SingleAsync(item => item.CourseId == course.Id, Cancellation);
        var row = await db.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == persisted.Id, Cancellation);
        using var json = JsonDocument.Parse(row.Payload); var payload = json.RootElement;
        LearningMessages.AssertSends("conteudo.versao-publicada.v1", payload);
        Assert.Equal(persisted.Id, payload.GetProperty("eventId").GetGuid());
        Assert.Equal(level, payload.GetProperty("level").GetString());
        Assert.Equal(prerequisites ? "Pedagogical description" : string.Empty, payload.GetProperty("description").GetString());
        Assert.Equal(prerequisites ? "Basics" : null, payload.GetProperty("prerequisite").GetProperty("text").GetString());
        var references = payload.GetProperty("prerequisite").GetProperty("recommendedCourses");
        Assert.Equal(prerequisites ? 2 : 0, references.GetArrayLength());
        if (prerequisites)
        {
            Assert.Equal(second.Id, references[0].GetProperty("courseId").GetGuid());
            Assert.Equal(first.Id, references[1].GetProperty("courseId").GetGuid());
            Assert.Equal("Second published title", references[0].GetProperty("title").GetString());
        }
        Assert.Equal(version.GetProperty("prerequisite").GetRawText(), (await VersionAsync(client, course.Id, 1)).GetProperty("prerequisite").GetRawText());
    }

    [Fact(DisplayName = nameof(LevelOnlyRepublicationCreatesNextVersionFactAndActWithSameLessons))]
    public async Task LevelOnlyRepublicationCreatesNextVersionFactAndActWithSameLessons()
        => await AssertRepublicationAsync(new { level = "intermediate" });

    [Fact(DisplayName = nameof(PrerequisiteOnlyRepublicationCreatesNextVersionFactAndActWithSameLessons))]
    public async Task PrerequisiteOnlyRepublicationCreatesNextVersionFactAndActWithSameLessons()
        => await AssertRepublicationAsync(new { prerequisiteText = "Git basics" });

    [Fact(DisplayName = nameof(SnapshotUsesCurrentPublishedTitleAndKeepsItAfterRecommendedCourseRepublication))]
    public async Task SnapshotUsesCurrentPublishedTitleAndKeepsItAfterRecommendedCourseRepublication()
    {
        var course = await SeedAsync(); var recommended = await SeedAsync(course.TenantId, "Published title"); using var client = Teacher(course.TenantId);
        await PublishAsync(client, recommended);
        await PatchAsync(client, recommended.Id, new { title = "Unpublished title" });
        await PatchAsync(client, course.Id, new { recommendedCourseIds = new[] { recommended.Id } });
        var snapshot = await PublishAsync(client, course);
        Assert.Equal("Published title", snapshot.GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("title").GetString());
        await PublishAsync(client, recommended);
        var read = await VersionAsync(client, course.Id, 1);
        Assert.Equal("Published title", read.GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("title").GetString());
        Assert.Equal("Unpublished title", (await ReadAsync(client, course.Id)).GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(DiscardRestoresPublishedLevelTextOrderedIdsAndCurrentLevel))]
    public async Task DiscardRestoresPublishedLevelTextOrderedIdsAndCurrentLevel()
    {
        var course = await SeedAsync(); var first = await SeedAsync(course.TenantId); var second = await SeedAsync(course.TenantId); using var client = Teacher(course.TenantId);
        await PublishAsync(client, first); await PublishAsync(client, second);
        await PatchAsync(client, course.Id, new { level = "beginner", prerequisiteText = "Basics", recommendedCourseIds = new[] { second.Id, first.Id } });
        await PublishAsync(client, course);
        await PatchAsync(client, course.Id, new { level = "advanced", prerequisiteText = "Changed", recommendedCourseIds = new[] { first.Id } });
        var restored = await DiscardAsync(client, course.Id);
        Assert.Equal("beginner", restored.GetProperty("level").GetString()); Assert.Equal("beginner", restored.GetProperty("currentLevel").GetString());
        Assert.Equal("Basics", restored.GetProperty("prerequisite").GetProperty("text").GetString());
        Assert.Equal(new[] { second.Id, first.Id }, restored.GetProperty("prerequisite").GetProperty("recommendedCourses").EnumerateArray().Select(item => item.GetProperty("courseId").GetGuid()));
        Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    [Fact(DisplayName = nameof(LegacyVersionMigratesToNullSnapshotAndDiscardRestoresEmptyAudience))]
    public async Task LegacyVersionMigratesToNullSnapshotAndDiscardRestoresEmptyAudience()
    {
        await using var legacy = new CourseLevelLegacyFixture(); await legacy.InitializeAsync(Cancellation);
        await using var db = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(legacy.ConnectionString).Options, new TenantContext());
        var course = await db.Courses.IgnoreQueryFilters().SingleAsync(item => item.Id == legacy.CourseId, Cancellation);
        var version = await db.CourseVersions.IgnoreQueryFilters().SingleAsync(item => item.CourseId == course.Id, Cancellation);
        Assert.Null(version.Level); Assert.Null(version.Prerequisite);
        var output = CodeForCoders.Learning.Application.UseCases.Courses.Common.CourseVersionOutput.FromVersion(version);
        Assert.Null(output.Level); Assert.Null(output.Prerequisite.Text); Assert.Empty(output.Prerequisite.RecommendedCourses);
        course.Update(new(null, null, false, null, null, Level: "advanced", HasLevel: true, PrerequisiteText: "Basics", HasPrerequisiteText: true));
        course.DiscardDraft(course.DraftRevision, version);
        Assert.Null(course.Level); Assert.Null(course.CurrentLevel); Assert.Null(course.PrerequisiteText); Assert.Empty(course.RecommendedCourseIds);
    }

    [Fact(DisplayName = nameof(RebuiltFingerprintFromPersistedSnapshotRecognizesUnchangedContent))]
    public async Task RebuiltFingerprintFromPersistedSnapshotRecognizesUnchangedContent()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        await PatchAsync(client, course.Id, new { level = "advanced", prerequisiteText = "Basics" }); await PublishAsync(client, course);
        await using (var db = Context()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE content.courses SET published_fingerprint = NULL WHERE id = {course.Id}", Cancellation);
        var unchanged = await PatchAsync(client, course.Id, new { level = "advanced", prerequisiteText = "Basics" });
        Assert.False(unchanged.GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    [Fact(DisplayName = nameof(DraftAudienceEditsCannotMutatePreviousVersion))]
    public async Task DraftAudienceEditsCannotMutatePreviousVersion()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        await PatchAsync(client, course.Id, new { level = "beginner", prerequisiteText = "Basics" }); await PublishAsync(client, course);
        var before = await VersionAsync(client, course.Id, 1);
        await PatchAsync(client, course.Id, new { level = "advanced", prerequisiteText = (string?)null });
        Assert.Equal(before.GetRawText(), (await VersionAsync(client, course.Id, 1)).GetRawText());
    }

    [Fact(DisplayName = nameof(RetryOfPublicationReturnsOriginalAudienceWithoutDuplicateFact))]
    public async Task RetryOfPublicationReturnsOriginalAudienceWithoutDuplicateFact()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        await PatchAsync(client, course.Id, new { level = "advanced" });
        var draft = await ReadAsync(client, course.Id); var key = Guid.CreateVersion7().ToString();
        var body = new { draftRevision = draft.GetProperty("draftRevision").GetInt32() };
        var first = await WriteAsync(client, course.Id, body, "/versions", key, HttpStatusCode.Created);
        var retry = await WriteAsync(client, course.Id, body, "/versions", key, HttpStatusCode.Created);
        Assert.Equal(first.GetRawText(), retry.GetRawText());
        await using var db = Context(); Assert.Equal(1, await db.CourseVersions.IgnoreQueryFilters().CountAsync(item => item.CourseId == course.Id, Cancellation));
    }

    private async Task AssertRepublicationAsync(object changes)
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var first = await PublishAsync(client, course);
        var changed = await PatchAsync(client, course.Id, changes); Assert.True(changed.GetProperty("hasUnpublishedChanges").GetBoolean());
        var second = await PublishAsync(client, course); Assert.Equal(2, second.GetProperty("versionNumber").GetInt32());
        Assert.Equal(first.GetProperty("modules").GetRawText(), second.GetProperty("modules").GetRawText());
        await using var db = Context(); var version = await db.CourseVersions.IgnoreQueryFilters().SingleAsync(item => item.CourseId == course.Id && item.VersionNumber == 2, Cancellation);
        var fact = await db.ContentOutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == version.Id, Cancellation);
        LearningMessages.AssertSends("conteudo.versao-publicada.v1", JsonDocument.Parse(fact.Payload).RootElement);
        var acts = await db.ContentOutboxMessages.IgnoreQueryFilters().Where(item => item.TenantId == course.TenantId && item.RoutingKey == "auditoria.ato-praticado.v1").ToListAsync(Cancellation);
        Assert.Equal(2, acts.Count); Assert.Contains(acts, item => JsonDocument.Parse(item.Payload).RootElement.GetProperty("fatoId").GetGuid() == version.Id);
    }

    private async Task<Course> SeedAsync(Guid? tenant = null, string title = "Publication course", string? description = null)
    {
        var course = Course.Create(new(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", title, description, DateTimeOffset.UtcNow));
        var module = course.AddModule(new("Module", null, false, null, null));
        course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
        await using var db = Context(); db.Courses.Add(course); await db.SaveChangesAsync(Cancellation); return course;
    }

    private static Task<JsonElement> ReadAsync(HttpClient client, Guid id) => client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{id}", Cancellation);
    private static Task<JsonElement> VersionAsync(HttpClient client, Guid id, int number) => client.GetFromJsonAsync<JsonElement>($"/internal/v1/courses/{id}/versions/{number}", Cancellation);
    private static Task<JsonElement> PatchAsync(HttpClient client, Guid id, object body) => WriteAsync(client, id, body);
    private static async Task<JsonElement> PublishAsync(HttpClient client, Course course)
    {
        var draft = await ReadAsync(client, course.Id);
        return await WriteAsync(client, course.Id, new { draftRevision = draft.GetProperty("draftRevision").GetInt32() }, "/versions", expected: HttpStatusCode.Created);
    }
    private static async Task<JsonElement> DiscardAsync(HttpClient client, Guid id)
    {
        var draft = await ReadAsync(client, id);
        return await WriteAsync(client, id, new { draftRevision = draft.GetProperty("draftRevision").GetInt32() }, "/discard-draft");
    }
    private static async Task<JsonElement> WriteAsync(HttpClient client, Guid id, object body, string suffix = "", string? key = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(suffix.Length == 0 ? HttpMethod.Patch : HttpMethod.Post, $"/internal/v1/courses/{id}{suffix}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString());
        using var response = await client.SendAsync(request, Cancellation); Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }
}
