using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(CourseApiCollection.Name)]
public sealed class CourseStructureTests(CourseApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private HttpClient Teacher(Guid? tenant = null) => factory.Actor(tenant ?? Guid.CreateVersion7(), Guid.CreateVersion7(), ["autoria.ler", "autoria.editar"]);
    private static string Path(Guid course) => $"/internal/v1/courses/{course:D}";
    private static JsonElement Modules(JsonElement course) => course.GetProperty("modules");
    private static Guid ModuleId(JsonElement course, int index = 0) => Modules(course)[index].GetProperty("moduleId").GetGuid();
    private static Guid LessonId(JsonElement course, int module = 0, int lesson = 0) => Modules(course)[module].GetProperty("lessons")[lesson].GetProperty("lessonId").GetGuid();

    [Fact(DisplayName = nameof(CoursePatchPreservesAbsentFieldsClearsDescriptionAndRecordsEditor))]
    public async Task CoursePatchPreservesAbsentFieldsClearsDescriptionAndRecordsEditor()
    {
        var tenant = Guid.CreateVersion7();
        using var teacher = Teacher(tenant);
        var course = await CreateAsync(teacher);
        using var colleague = Teacher(tenant);
        colleague.DefaultRequestHeaders.Remove("X-Actor-Name"); colleague.DefaultRequestHeaders.Add("X-Actor-Name", "Colleague");
        var updated = await WriteAsync(colleague, HttpMethod.Patch, Path(course), new { title = "Renamed" });
        Assert.Equal("Initial description", updated.GetProperty("description").GetString());
        Assert.Equal("Colleague", updated.GetProperty("lastEditedBy").GetProperty("name").GetString());
        Assert.Equal(2, updated.GetProperty("draftRevision").GetInt32());
        updated = await WriteAsync(teacher, HttpMethod.Patch, Path(course), new { description = (string?)null });
        Assert.Equal("Renamed", updated.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("description").ValueKind);
        var loaded = await GetAsync(colleague, course);
        Assert.Equal(updated.GetProperty("title").GetString(), loaded.GetProperty("title").GetString());
        Assert.Equal(updated.GetProperty("draftRevision").GetInt32(), loaded.GetProperty("draftRevision").GetInt32());
        Assert.Equal(updated.GetProperty("description").ValueKind, loaded.GetProperty("description").ValueKind);
        Assert.True((updated.GetProperty("lastEditedAt").GetDateTimeOffset() - loaded.GetProperty("lastEditedAt").GetDateTimeOffset()).Duration() < TimeSpan.FromMicroseconds(1));
    }

    [Fact(DisplayName = nameof(ModuleInsertionRenameAndReorderingPreserveIdentityAndContiguousPositions))]
    public async Task ModuleInsertionRenameAndReorderingPreserveIdentityAndContiguousPositions()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "First" });
        var first = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Second", position = 1 });
        var second = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Patch, Path(course) + $"/modules/{first}", new { title = "Renamed", position = 1 });
        Assert.Equal(first, ModuleId(result)); Assert.Equal(second, ModuleId(result, 1));
        Assert.Equal("Renamed", Modules(result)[0].GetProperty("title").GetString());
        AssertPositions(Modules(await GetAsync(teacher, course)));
    }

    [Fact(DisplayName = nameof(LessonInsertionRenameAndReorderingPreserveIdentity))]
    public async Task LessonInsertionRenameAndReorderingPreserveIdentity()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Module" });
        var module = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "First", description = "Lesson description" });
        var first = LessonId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Second", position = 1 });
        var second = LessonId(result);
        result = await WriteAsync(teacher, HttpMethod.Patch, Path(course) + $"/lessons/{first}", new { title = "Renamed", position = 1 });
        Assert.Equal(first, LessonId(result)); Assert.Equal(second, LessonId(result, 0, 1));
        Assert.Equal("Lesson description", Modules(result)[0].GetProperty("lessons")[0].GetProperty("description").GetString());
        AssertPositions(Modules(await GetAsync(teacher, course))[0].GetProperty("lessons"));
    }

    [Fact(DisplayName = nameof(MovingLessonBetweenModulesPreservesIdAndRenumbersBothParents))]
    public async Task MovingLessonBetweenModulesPreservesIdAndRenumbersBothParents()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Source" }); var source = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Destination" }); var destination = ModuleId(result, 1);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{source}/lessons", new { title = "Move me" }); var lesson = LessonId(result);
        await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{source}/lessons", new { title = "Stay" });
        await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{destination}/lessons", new { title = "Destination first" });
        result = await WriteAsync(teacher, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { moduleId = destination });
        Assert.Equal(lesson, LessonId(result, 1, 1));
        var loaded = await GetAsync(teacher, course);
        Assert.Equal(lesson, LessonId(loaded, 1, 1));
        foreach (var module in Modules(loaded).EnumerateArray()) AssertPositions(module.GetProperty("lessons"));
        result = await WriteAsync(teacher, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { moduleId = source, position = 1 });
        Assert.Equal(lesson, LessonId(result));
    }

    [Fact(DisplayName = nameof(RemovingAndRecreatingLessonGivesNewIdentityAndModuleDeletionCascades))]
    public async Task RemovingAndRecreatingLessonGivesNewIdentityAndModuleDeletionCascades()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Remove" }); var module = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Same title" }); var removed = LessonId(result);
        await WriteAsync(teacher, HttpMethod.Delete, Path(course) + $"/lessons/{removed}");
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Same title" });
        Assert.NotEqual(removed, LessonId(result)); var replacement = LessonId(result);
        await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Remain" });
        result = await WriteAsync(teacher, HttpMethod.Delete, Path(course) + $"/modules/{module}");
        Assert.Single(Modules(result).EnumerateArray()); AssertPositions(Modules(result));
        await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course) + $"/lessons/{replacement}", new { title = "Missing" }, HttpStatusCode.NotFound, "LESSON_NOT_FOUND");
    }

    [Fact(DisplayName = nameof(InvalidPositionsAndBlankTitlesDoNotMutateDraft))]
    public async Task InvalidPositionsAndBlankTitlesDoNotMutateDraft()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Module" }); var module = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "Lesson" }); var lesson = LessonId(result);
        await RejectUnchangedAsync(teacher, course, HttpMethod.Post, Path(course) + "/modules", new { title = "Outside", position = 3 }, HttpStatusCode.UnprocessableEntity, "INVALID_POSITION");
        await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course) + $"/modules/{module}", new { position = 0 }, HttpStatusCode.UnprocessableEntity, "INVALID_POSITION");
        await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { position = 2 }, HttpStatusCode.UnprocessableEntity, "INVALID_POSITION");
        await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course), new { title = "   " }, HttpStatusCode.UnprocessableEntity, "TITLE_REQUIRED");
        await RejectUnchangedAsync(teacher, course, HttpMethod.Post, Path(course) + $"/modules/{module}/lessons", new { title = "" }, HttpStatusCode.UnprocessableEntity, "TITLE_REQUIRED");
    }

    [Fact(DisplayName = nameof(OtherCourseAndTenantItemsAreNotFoundAndReaderCannotEdit))]
    public async Task OtherCourseAndTenantItemsAreNotFoundAndReaderCannotEdit()
    {
        var tenant = Guid.CreateVersion7(); using var teacher = Teacher(tenant);
        var first = await CreateAsync(teacher); var second = await CreateAsync(teacher);
        var result = await WriteAsync(teacher, HttpMethod.Post, Path(first) + "/modules", new { title = "Private" }); var module = ModuleId(result);
        result = await WriteAsync(teacher, HttpMethod.Post, Path(first) + $"/modules/{module}/lessons", new { title = "Private lesson" }); var lesson = LessonId(result);
        await RejectUnchangedAsync(teacher, second, HttpMethod.Patch, Path(second) + $"/modules/{module}", new { title = "Cross course" }, HttpStatusCode.NotFound, "MODULE_NOT_FOUND");
        await RejectUnchangedAsync(teacher, second, HttpMethod.Delete, Path(second) + $"/lessons/{lesson}", null, HttpStatusCode.NotFound, "LESSON_NOT_FOUND");
        using var stranger = Teacher(); using var denied = await SendAsync(stranger, HttpMethod.Delete, Path(first) + $"/modules/{module}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var reader = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler"]);
        using var readOnly = await SendAsync(reader, HttpMethod.Delete, Path(first) + $"/modules/{module}");
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Single(Modules(await GetAsync(teacher, first)).EnumerateArray());
    }

    [Fact(DisplayName = nameof(StructureLimitsRejectCreationAndMovementWithoutMutation))]
    public async Task StructureLimitsRejectCreationAndMovementWithoutMutation()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher); Guid first = default, second = default;
        for (var index = 0; index < 100; index++)
        {
            var result = await WriteAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = $"Module {index}" });
            if (index == 0) first = ModuleId(result); if (index == 1) second = ModuleId(result, 1);
        }
        await RejectUnchangedAsync(teacher, course, HttpMethod.Post, Path(course) + "/modules", new { title = "Overflow" }, HttpStatusCode.UnprocessableEntity, "STRUCTURE_LIMIT_REACHED");
        for (var index = 0; index < 200; index++) await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{first}/lessons", new { title = $"Lesson {index}" });
        await RejectUnchangedAsync(teacher, course, HttpMethod.Post, Path(course) + $"/modules/{first}/lessons", new { title = "Overflow" }, HttpStatusCode.UnprocessableEntity, "STRUCTURE_LIMIT_REACHED");
        var added = await WriteAsync(teacher, HttpMethod.Post, Path(course) + $"/modules/{second}/lessons", new { title = "Cannot move" }); var lesson = LessonId(added, 1);
        await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course) + $"/lessons/{lesson}", new { moduleId = first }, HttpStatusCode.UnprocessableEntity, "STRUCTURE_LIMIT_REACHED");
    }

    [Fact(DisplayName = nameof(ConcurrentEditsDoNotLoseItemsAndRetriesPreserveResponseAndLocation))]
    public async Task ConcurrentEditsDoNotLoseItemsAndRetriesPreserveResponseAndLocation()
    {
        var tenant = Guid.CreateVersion7(); using var first = Teacher(tenant); using var second = Teacher(tenant); var course = await CreateAsync(first);
        var responses = await Task.WhenAll(SendAsync(first, HttpMethod.Post, Path(course) + "/modules", new { title = "A" }, "repeat"), SendAsync(second, HttpMethod.Post, Path(course) + "/modules", new { title = "B" }));
        using var created = responses[0]; using var colleague = responses[1];
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.Equal(HttpStatusCode.Created, colleague.StatusCode);
        var original = await ReadAsync(created);
        using var replay = await SendAsync(first, HttpMethod.Post, Path(course) + "/modules", new { title = "A" }, "repeat");
        Assert.Equal(original.GetRawText(), (await ReadAsync(replay)).GetRawText());
        Assert.Equal(created.Headers.Location, replay.Headers.Location);
        using var formattedRequest = new HttpRequestMessage(HttpMethod.Post, Path(course) + "/modules")
        { Content = new StringContent(" { \"title\": \"A\" } ", System.Text.Encoding.UTF8, "application/json") };
        formattedRequest.Headers.Add("Idempotency-Key", "repeat");
        using var formattedReplay = await first.SendAsync(formattedRequest, Cancellation);
        Assert.Equal(HttpStatusCode.Created, formattedReplay.StatusCode);
        Assert.Equal(original.GetRawText(), (await ReadAsync(formattedReplay)).GetRawText());
        var loaded = await GetAsync(second, course); Assert.Equal(2, Modules(loaded).GetArrayLength()); AssertPositions(Modules(loaded));
        Assert.Equal(3, loaded.GetProperty("draftRevision").GetInt32());
        await RejectUnchangedAsync(first, course, HttpMethod.Post, Path(course) + "/modules", new { title = "Changed" }, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED", "repeat");
    }

    [Fact(DisplayName = nameof(MalformedPatchMissingKeyAndUnknownPropertiesAreBadRequests))]
    public async Task MalformedPatchMissingKeyAndUnknownPropertiesAreBadRequests()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        foreach (var body in new object[] { new { }, new { title = (string?)null }, new { price = 10 }, new { title = new string('x', 201) } })
            await RejectUnchangedAsync(teacher, course, HttpMethod.Patch, Path(course), body, HttpStatusCode.BadRequest, "INVALID_REQUEST");
        using var missing = await teacher.PatchAsJsonAsync(Path(course), new { title = "Missing key" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact(DisplayName = nameof(CreationAndEditingHaveSeparateIntentNamespaces))]
    public async Task CreationAndEditingHaveSeparateIntentNamespaces()
    {
        using var teacher = Teacher(); var course = await CreateAsync(teacher);
        var editKey = "separate-intent";
        var namespacedKey = "edit:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{course:D}:create-module:{editKey}")));
        using var creation = await SendAsync(teacher, HttpMethod.Post, "/internal/v1/courses", new { title = "Another course" }, namespacedKey);
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        using var edited = await SendAsync(teacher, HttpMethod.Post, Path(course) + "/modules", new { title = "Module" }, editKey);
        Assert.Equal(HttpStatusCode.Created, edited.StatusCode);
        Assert.Single(Modules(await GetAsync(teacher, course)).EnumerateArray());
        using var replay = await SendAsync(teacher, HttpMethod.Post, "/internal/v1/courses", new { title = "Another course" }, namespacedKey);
        Assert.Equal((await ReadAsync(creation)).GetRawText(), (await ReadAsync(replay)).GetRawText());
    }

    private static void AssertPositions(JsonElement items) => Assert.Equal(Enumerable.Range(1, items.GetArrayLength()), items.EnumerateArray().Select(item => item.GetProperty("position").GetInt32()));
    private static async Task<Guid> CreateAsync(HttpClient teacher) => (await WriteAsync(teacher, HttpMethod.Post, "/internal/v1/courses", new { title = "Course", description = "Initial description" })).GetProperty("courseId").GetGuid();
    private static async Task<JsonElement> GetAsync(HttpClient teacher, Guid course)
    {
        using var response = await teacher.GetAsync(Path(course), Cancellation); response.EnsureSuccessStatusCode(); return await ReadAsync(response);
    }
    private static async Task<HttpResponseMessage> SendAsync(HttpClient teacher, HttpMethod method, string path, object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.CreateVersion7().ToString()); return await teacher.SendAsync(request, Cancellation);
    }
    private static async Task<JsonElement> WriteAsync(HttpClient teacher, HttpMethod method, string path, object? body = null)
    {
        using var response = await SendAsync(teacher, method, path, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Cancellation));
        return await ReadAsync(response);
    }
    private static async Task RejectUnchangedAsync(HttpClient teacher, Guid course, HttpMethod method, string path, object? body, HttpStatusCode status, string code, string? key = null)
    {
        var before = await GetAsync(teacher, course);
        using var response = await SendAsync(teacher, method, path, body, key);
        Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Equal(before.GetRawText(), (await GetAsync(teacher, course)).GetRawText());
    }
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Cancellation), cancellationToken: Cancellation);
        return document.RootElement.Clone();
    }
}
