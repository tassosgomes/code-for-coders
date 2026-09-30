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
public sealed class CourseLevelTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private HttpClient Teacher(Guid tenant) => factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
    private LearningDbContext Context() => new(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(factory.DatabaseConnection).Options, new TenantContext());

    [Fact(DisplayName = nameof(EveryLevelPersistsAndIsReturnedByDirectRead))]
    public async Task EveryLevelPersistsAndIsReturnedByDirectRead()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        foreach (var level in new[] { "beginner", "intermediate", "advanced" })
        {
            using var response = await WriteAsync(client, course.Id, new { level });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(level, (await ReadAsync(client, course.Id)).GetProperty("level").GetString());
        }
    }

    [Fact(DisplayName = nameof(AbsentLevelPreservesAndExplicitNullClears))]
    public async Task AbsentLevelPreservesAndExplicitNullClears()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId);
        using var set = await WriteAsync(client, course.Id, new { level = "beginner" }); set.EnsureSuccessStatusCode();
        using var rename = await WriteAsync(client, course.Id, new { title = "Renamed" }); rename.EnsureSuccessStatusCode();
        Assert.Equal("beginner", (await ReadAsync(client, course.Id)).GetProperty("level").GetString());
        using var clear = await WriteAsync(client, course.Id, new { level = (string?)null }); clear.EnsureSuccessStatusCode();
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(client, course.Id)).GetProperty("level").ValueKind);
    }

    [Fact(DisplayName = nameof(ReplayReturnsOriginalResponseWithoutAnotherRevision))]
    public async Task ReplayReturnsOriginalResponseWithoutAnotherRevision()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var key = Guid.CreateVersion7().ToString();
        using var first = await WriteAsync(client, course.Id, new { level = "intermediate" }, key);
        using var replay = await WriteAsync(client, course.Id, new { level = "intermediate" }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(2, (await ReadAsync(client, course.Id)).GetProperty("draftRevision").GetInt32());
        using var reused = await WriteAsync(client, course.Id, new { level = "advanced" }, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await reused.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(InvalidLevelReportsFieldAndDoesNotChangeAnyContent))]
    public async Task InvalidLevelReportsFieldAndDoesNotChangeAnyContent()
    {
        var course = await SeedAsync(); using var client = Teacher(course.TenantId); var before = await ReadAsync(client, course.Id);
        foreach (var level in new object[] { "expert", 42, true, new[] { "beginner" } })
        {
            using var response = await WriteAsync(client, course.Id, new { level, title = "Rejected" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.Equal("FIELD_INVALID", problem.GetProperty("code").GetString());
            Assert.Contains("level", problem.GetProperty("detail").GetString()); Assert.True(problem.GetProperty("errors").TryGetProperty("level", out _));
        }
        Assert.Equal(before.GetRawText(), (await ReadAsync(client, course.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(ReaderJwtCannotEditLevel))]
    public async Task ReaderJwtCannotEditLevel()
    {
        var course = await SeedAsync(); using var teacher = Teacher(course.TenantId); var before = await ReadAsync(teacher, course.Id);
        using var reader = factory.Actor(course.TenantId, Guid.CreateVersion7(), ["autoria.ler"]);
        using var response = await WriteAsync(reader, course.Id, new { level = "advanced" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before.GetRawText(), (await ReadAsync(teacher, course.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(OtherTenantSeesNoCourseAndCannotEditLevel))]
    public async Task OtherTenantSeesNoCourseAndCannotEditLevel()
    {
        var course = await SeedAsync(); using var teacher = Teacher(course.TenantId); var before = await ReadAsync(teacher, course.Id);
        using var stranger = Teacher(Guid.CreateVersion7());
        using var response = await WriteAsync(stranger, course.Id, new { level = "advanced" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before.GetRawText(), (await ReadAsync(teacher, course.Id)).GetRawText());
    }

    [Fact(DisplayName = nameof(LevelOnlyEditAndRevertUpdateRevisionWithoutChangingPublishedSnapshot))]
    public async Task LevelOnlyEditAndRevertUpdateRevisionWithoutChangingPublishedSnapshot()
    {
        var course = await SeedAsync(published: true); using var client = Teacher(course.TenantId);
        var snapshot = await client.GetStringAsync($"/internal/v1/courses/{course.Id}/versions/1", Cancellation);
        using var edit = await WriteAsync(client, course.Id, new { level = "advanced" });
        var changed = await edit.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(changed.GetProperty("hasUnpublishedChanges").GetBoolean()); Assert.Equal(2, changed.GetProperty("draftRevision").GetInt32());
        Assert.Equal(JsonValueKind.Null, changed.GetProperty("currentLevel").ValueKind);
        Assert.Equal(snapshot, await client.GetStringAsync($"/internal/v1/courses/{course.Id}/versions/1", Cancellation));
        using var revert = await WriteAsync(client, course.Id, new { level = (string?)null });
        var restored = await revert.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean()); Assert.Equal(3, restored.GetProperty("draftRevision").GetInt32());
    }

    [Fact(DisplayName = nameof(DiscardClearsDraftLevelAndListProjectsCurrentLevel))]
    public async Task DiscardClearsDraftLevelAndListProjectsCurrentLevel()
    {
        var course = await SeedAsync(published: true); using var client = Teacher(course.TenantId);
        using var edit = await WriteAsync(client, course.Id, new { level = "beginner" }); edit.EnsureSuccessStatusCode();
        using var discard = await WriteAsync(client, course.Id, new { draftRevision = 2 }, suffix: "/discard-draft");
        var restored = await discard.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(HttpStatusCode.OK, discard.StatusCode); Assert.Equal(JsonValueKind.Null, restored.GetProperty("level").ValueKind);
        Assert.False(restored.GetProperty("hasUnpublishedChanges").GetBoolean());
        var list = await client.GetFromJsonAsync<JsonElement>("/internal/v1/courses", Cancellation);
        Assert.Equal(JsonValueKind.Null, list.GetProperty("data")[0].GetProperty("currentLevel").ValueKind);
    }

    [Fact(DisplayName = nameof(LegacyMigrationResetsFingerprintPreservesIndicatorAndLazyEditCanRevert))]
    public async Task LegacyMigrationResetsFingerprintPreservesIndicatorAndLazyEditCanRevert()
    {
        await using var legacy = new CourseLevelLegacyFixture(); await legacy.InitializeAsync(Cancellation);
        await using var api = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:DefaultConnection", legacy.ConnectionString));
        using var identity = Teacher(legacy.TenantId); using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Add("X-Actor-Name", "Teacher");
        using var edit = await WriteAsync(client, legacy.CourseId, new { level = "beginner" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode); Assert.True((await edit.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
        using var revert = await WriteAsync(client, legacy.CourseId, new { level = (string?)null });
        Assert.Equal(HttpStatusCode.OK, revert.StatusCode); Assert.False((await revert.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("hasUnpublishedChanges").GetBoolean());
    }

    private async Task<Course> SeedAsync(bool published = false)
    {
        var course = Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Level course", "Description", DateTimeOffset.UtcNow));
        await using var context = Context(); context.Courses.Add(course);
        if (published)
        {
            var module = course.AddModule(new("Module", null, false, null, null));
            course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
            context.CourseVersions.Add(course.Publish(new(1, null, new(course.TenantId, course.CreatedById, "Teacher", course.Title, course.Description, DateTimeOffset.UtcNow))));
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
