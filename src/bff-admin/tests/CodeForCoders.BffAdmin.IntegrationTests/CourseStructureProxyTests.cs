using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseStructureProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealClientAndHostForwardAllEditsWithServerIdentityAndWholeDraft))]
    public async Task RealClientAndHostForwardAllEditsWithServerIdentityAndWholeDraft()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        Assert.IsType<CourseAuthoringClient>(factory.Services.GetRequiredService<ICourseAuthoringClient>());
        client.DefaultRequestHeaders.Authorization = new("Bearer", "forged-browser-token");
        client.DefaultRequestHeaders.Add("X-Actor-Name", "Forged actor");
        var course = factory.Learning.CourseId; var module = Guid.CreateVersion7(); var lesson = Guid.CreateVersion7();
        factory.Learning.Modules = [JsonSerializer.SerializeToElement(new { moduleId = module, title = "Module", position = 1,
            lessons = new[] { new { lessonId = lesson, title = "Lesson", position = 1, videoId = (Guid?)null, description = "Description" } } })];
        var edits = new (HttpMethod Method, string Suffix, object? Body)[] {
            (HttpMethod.Patch, "", new { title = "Edited course", description = (string?)null }),
            (HttpMethod.Post, "/modules", new { title = "Module", position = 1 }),
            (HttpMethod.Patch, $"/modules/{module:D}", new { position = 1 }),
            (HttpMethod.Post, $"/modules/{module:D}/lessons", new { title = "Lesson", description = "Description" }),
            (HttpMethod.Patch, $"/lessons/{lesson:D}", new { moduleId = module, position = 1 }),
            (HttpMethod.Delete, $"/lessons/{lesson:D}", null),
            (HttpMethod.Delete, $"/modules/{module:D}", null) };
        foreach (var edit in edits)
        {
            using var response = await SendAsync(client, edit.Method, $"/api/v1/courses/{course:D}" + edit.Suffix, edit.Body);
            Assert.Equal(edit.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(edit.Method, factory.Learning.Method);
            Assert.Equal($"/internal/v1/courses/{course:D}" + edit.Suffix, factory.Learning.Uri!.AbsolutePath);
            Assert.Equal("server-learning-token", factory.Learning.Token); Assert.Equal("Validated teacher", factory.Learning.ActorName);
            Assert.Equal("structure-intent", factory.Learning.Key); Assert.Equal("learning", factory.Identity.LastAudience);
            if (edit.Body is not null) Assert.Equal(JsonSerializer.SerializeToElement(edit.Body).GetRawText(), factory.Learning.Payload!.Value.GetRawText());
            using var draft = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Cancellation), cancellationToken: Cancellation);
            Assert.Equal(course, draft.RootElement.GetProperty("courseId").GetGuid());
            var mappedLesson = draft.RootElement.GetProperty("modules")[0].GetProperty("lessons")[0];
            Assert.Equal(lesson, mappedLesson.GetProperty("lessonId").GetGuid());
            Assert.Equal(JsonValueKind.Null, mappedLesson.GetProperty("video").ValueKind); Assert.False(mappedLesson.TryGetProperty("videoId", out _));
        }
    }

    [Fact(DisplayName = nameof(StructureCreatesPreserveUpstreamItemLocation))]
    public async Task StructureCreatesPreserveUpstreamItemLocation()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        var path = $"/courses/{factory.Learning.CourseId:D}/modules/{Guid.CreateVersion7():D}";
        factory.Learning.Location = "/internal/v1" + path;
        using var module = await SendAsync(client, HttpMethod.Post, $"/api/v1/courses/{factory.Learning.CourseId:D}/modules", new { title = "New module" });
        Assert.Equal("/api/v1" + path, module.Headers.Location?.OriginalString);
        path += $"/lessons/{Guid.CreateVersion7():D}"; factory.Learning.Location = "/internal/v1" + path;
        using var lesson = await SendAsync(client, HttpMethod.Post, $"/api/v1/courses/{factory.Learning.CourseId:D}/modules/{Guid.CreateVersion7():D}/lessons", new { title = "New lesson" });
        Assert.Equal("/api/v1" + path, lesson.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = nameof(ReaderMissingCsrfAndMissingIntentCannotReachLearningEvenForDeletes))]
    public async Task ReaderMissingCsrfAndMissingIntentCannotReachLearningEvenForDeletes()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        var path = $"/api/v1/courses/{factory.Learning.CourseId:D}/modules/{Guid.CreateVersion7():D}";
        factory.Identity.Permissions = ["autoria.ler"];
        using var reader = await SendAsync(client, HttpMethod.Delete, path); Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
        factory.Identity.Permissions = ["autoria.ler", "autoria.editar"];
        using var missingIntent = await client.DeleteAsync(path, Cancellation); Assert.Equal(HttpStatusCode.BadRequest, missingIntent.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        using var csrf = await SendAsync(client, HttpMethod.Delete, path); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        Assert.Equal(0, factory.Learning.Calls);
    }

    [Fact(DisplayName = nameof(BusinessProblemsSurviveProxyAndUpstreamFailuresAreSanitized))]
    public async Task BusinessProblemsSurviveProxyAndUpstreamFailuresAreSanitized()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        var path = $"/api/v1/courses/{factory.Learning.CourseId:D}/lessons/{Guid.CreateVersion7():D}";
        foreach (var problem in new[] { (HttpStatusCode.NotFound, "LESSON_NOT_FOUND"), (HttpStatusCode.UnprocessableEntity, "INVALID_POSITION"), (HttpStatusCode.UnprocessableEntity, "STRUCTURE_LIMIT_REACHED") })
        {
            factory.Learning.Status = problem.Item1; factory.Learning.ProblemCode = problem.Item2;
            using var rejected = await SendAsync(client, HttpMethod.Patch, path, new { position = 0 });
            Assert.Equal(problem.Item1, rejected.StatusCode); Assert.Contains(problem.Item2, await rejected.Content.ReadAsStringAsync(Cancellation));
        }
        factory.Learning.Status = HttpStatusCode.InternalServerError; factory.Learning.ProblemCode = "internal-database-detail";
        using var unavailable = await SendAsync(client, HttpMethod.Patch, path, new { position = 1 });
        Assert.Equal(HttpStatusCode.BadGateway, unavailable.StatusCode);
        var content = await unavailable.Content.ReadAsStringAsync(Cancellation); Assert.Contains("LEARNING_UNAVAILABLE", content); Assert.DoesNotContain("internal-database-detail", content);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", "structure-intent"); return await client.SendAsync(request, Cancellation);
    }
}
