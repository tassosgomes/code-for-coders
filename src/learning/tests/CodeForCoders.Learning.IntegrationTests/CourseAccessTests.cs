using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

[Collection(CourseApiCollection.Name)]
public sealed class CourseAccessTests(CourseApiFactory factory)
{
    private static readonly string[] Teacher = ["autoria.ler", "autoria.editar"];
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(TeacherCreatesAndAnotherTeacherFindsTheSchoolCourse))]
    public async Task TeacherCreatesAndAnotherTeacherFindsTheSchoolCourse()
    {
        var tenant = Guid.CreateVersion7();
        using var author = factory.Actor(tenant, Guid.CreateVersion7(), Teacher);
        using var created = await CreateAsync(author, "API course", "intent-one");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var course = await ReadAsync(created);
        var id = course.GetProperty("courseId").GetGuid();
        Assert.Equal($"/internal/v1/courses/{id:D}", created.Headers.Location?.OriginalString);
        Assert.Equal(1, course.GetProperty("draftRevision").GetInt32());
        Assert.Equal("draft", course.GetProperty("status").GetString());
        Assert.False(course.GetProperty("hasUnpublishedChanges").GetBoolean());
        Assert.Empty(course.GetProperty("modules").EnumerateArray());
        using var colleague = factory.Actor(tenant, Guid.CreateVersion7(), Teacher);
        using var list = await colleague.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(id, (await ReadAsync(list)).GetProperty("data")[0].GetProperty("courseId").GetGuid());
        using var detail = await colleague.GetAsync(created.Headers.Location, Cancellation);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("Teacher display name", (await ReadAsync(detail)).GetProperty("createdBy").GetProperty("name").GetString());
    }

    [Fact(DisplayName = nameof(ReaderReadsButDirectWritingIsForbidden))]
    public async Task ReaderReadsButDirectWritingIsForbidden()
    {
        var tenant = Guid.CreateVersion7();
        using var teacher = factory.Actor(tenant, Guid.CreateVersion7(), Teacher);
        using var created = await CreateAsync(teacher, "Read only", "read-course");
        using var reader = factory.Actor(tenant, Guid.CreateVersion7(), ["autoria.ler"]);
        using var detail = await reader.GetAsync(created.Headers.Location, Cancellation);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var list = await reader.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var denied = await CreateAsync(reader, "Forbidden", "no-write");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact(DisplayName = nameof(OtherTenantCannotListOrRetrieveTheCourse))]
    public async Task OtherTenantCannotListOrRetrieveTheCourse()
    {
        using var teacher = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        using var created = await CreateAsync(teacher, "Private title", "cross-tenant");
        using var stranger = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        using var list = await stranger.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Empty((await ReadAsync(list)).GetProperty("data").EnumerateArray());
        using var detail = await stranger.GetAsync(created.Headers.Location, Cancellation);
        using var missing = await stranger.GetAsync($"/internal/v1/courses/{Guid.CreateVersion7()}", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(missing.StatusCode, detail.StatusCode);
        Assert.Equal("COURSE_NOT_FOUND", (await ReadAsync(detail)).GetProperty("code").GetString());
        Assert.DoesNotContain("Private title", await detail.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentRetriesReturnTheSameCreationAndChangedBodyIsRejected))]
    public async Task ConcurrentRetriesReturnTheSameCreationAndChangedBodyIsRejected()
    {
        using var teacher = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        var responses = await Task.WhenAll(CreateAsync(teacher, "Idempotent", "same-intent"), CreateAsync(teacher, "Idempotent", "same-intent"));
        using var first = responses[0]; using var second = responses[1];
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetRawText(), (await ReadAsync(second)).GetRawText());
        using var list = await teacher.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Equal(1, (await ReadAsync(list)).GetProperty("pagination").GetProperty("total").GetInt32());
        using var changed = await CreateAsync(teacher, "Different", "same-intent");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await ReadAsync(changed)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(BlankTitleIsRejectedWithFieldAndDoesNotConsumeTheIntent))]
    public async Task BlankTitleIsRejectedWithFieldAndDoesNotConsumeTheIntent()
    {
        using var teacher = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        using var blank = await CreateAsync(teacher, "   ", "correctable");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blank.StatusCode);
        var problem = await ReadAsync(blank);
        Assert.Equal("TITLE_REQUIRED", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("title", out _));
        using var corrected = await CreateAsync(teacher, "Corrected", "correctable");
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
    }

    [Fact(DisplayName = nameof(ExpiredWrongAudienceMissingIdentityAndNoPermissionAreRejected))]
    public async Task ExpiredWrongAudienceMissingIdentityAndNoPermissionAreRejected()
    {
        var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7();
        using var expired = factory.Actor(tenant, actor, Teacher, expired: true);
        using var wrongAudience = factory.Actor(tenant, actor, Teacher, "media");
        using var noTenant = factory.Actor(Guid.Empty, actor, Teacher);
        using var noActor = factory.Actor(tenant, Guid.Empty, Teacher);
        using var noPermission = factory.Actor(tenant, actor, []);
        foreach (var client in new[] { expired, wrongAudience, noTenant, noActor })
        {
            using var response = await client.GetAsync("/internal/v1/courses", Cancellation);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var forbidden = await noPermission.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact(DisplayName = nameof(ListIsPaginatedAndOrderedByLatestEditAndFilter))]
    public async Task ListIsPaginatedAndOrderedByLatestEditAndFilter()
    {
        using var teacher = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        using var older = await CreateAsync(teacher, "Older", "older");
        using var newer = await CreateAsync(teacher, "Newer", "newer");
        using var pageOne = await teacher.GetAsync("/internal/v1/courses?_page=1&_size=1&status=draft", Cancellation);
        var page = await ReadAsync(pageOne);
        Assert.Equal("Newer", page.GetProperty("data")[0].GetProperty("title").GetString());
        Assert.Equal(2, page.GetProperty("pagination").GetProperty("totalPages").GetInt32());
        using var pageTwo = await teacher.GetAsync("/internal/v1/courses?_page=2&_size=1", Cancellation);
        Assert.Equal("Older", (await ReadAsync(pageTwo)).GetProperty("data")[0].GetProperty("title").GetString());
        using var published = await teacher.GetAsync("/internal/v1/courses?status=published", Cancellation);
        Assert.Empty((await ReadAsync(published)).GetProperty("data").EnumerateArray());
        using var invalid = await teacher.GetAsync("/internal/v1/courses?_page=0", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact(DisplayName = nameof(SameIntentKeyIsIsolatedByActor))]
    public async Task SameIntentKeyIsIsolatedByActor()
    {
        var tenant = Guid.CreateVersion7();
        using var teacherA = factory.Actor(tenant, Guid.CreateVersion7(), Teacher);
        using var teacherB = factory.Actor(tenant, Guid.CreateVersion7(), Teacher);
        using var courseA = await CreateAsync(teacherA, "A", "shared-key");
        using var courseB = await CreateAsync(teacherB, "B", "shared-key");
        Assert.Equal(HttpStatusCode.Created, courseB.StatusCode);
        Assert.NotEqual((await ReadAsync(courseA)).GetProperty("courseId").GetGuid(), (await ReadAsync(courseB)).GetProperty("courseId").GetGuid());
    }

    [Fact(DisplayName = nameof(RequestLimitsAndUnknownFieldsAreRejectedWithoutPersisting))]
    public async Task RequestLimitsAndUnknownFieldsAreRejectedWithoutPersisting()
    {
        using var teacher = factory.Actor(Guid.CreateVersion7(), Guid.CreateVersion7(), Teacher);
        using var tooLong = await CreateAsync(teacher, new string('x', 201), "too-long");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var missingKey = await teacher.PostAsJsonAsync("/internal/v1/courses", new { title = "Missing key" }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        using var unknown = await teacher.PostAsJsonAsync("/internal/v1/courses", new { title = "Unknown field", price = 100 }, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using var list = await teacher.GetAsync("/internal/v1/courses", Cancellation);
        Assert.Empty((await ReadAsync(list)).GetProperty("data").EnumerateArray());
        using var boundary = await CreateAsync(teacher, new string('x', 200), "maximum-title");
        Assert.Equal(HttpStatusCode.Created, boundary.StatusCode);
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string title, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/courses") { Content = JsonContent.Create(new { title, description = "Pedagogical description" }) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Cancellation);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Cancellation), cancellationToken: Cancellation);
        return document.RootElement.Clone();
    }
}
