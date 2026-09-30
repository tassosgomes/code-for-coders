using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseLevelProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealLearningClientPreservesDraftAndCurrentLevelInReadsAndList))]
    public async Task RealLearningClientPreservesDraftAndCurrentLevelInReadsAndList()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        Assert.IsType<CourseAuthoringClient>(factory.Services.GetRequiredService<ICourseAuthoringClient>());
        factory.Learning.Level = "advanced"; factory.Learning.CurrentLevel = "beginner";
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal("advanced", detail.GetProperty("level").GetString()); Assert.Equal("beginner", detail.GetProperty("currentLevel").GetString());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/courses?_page=1&_size=20", Cancellation);
        Assert.Equal("beginner", list.GetProperty("data")[0].GetProperty("currentLevel").GetString());
        factory.Learning.CurrentLevel = null;
        list = await client.GetFromJsonAsync<JsonElement>("/api/v1/courses?_page=1&_size=20", Cancellation);
        Assert.Equal(JsonValueKind.Null, list.GetProperty("data")[0].GetProperty("currentLevel").ValueKind);
    }

    [Fact(DisplayName = nameof(LevelPatchForwardsIntentServerIdentityAndFullResponse))]
    public async Task LevelPatchForwardsIntentServerIdentityAndFullResponse()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "browser-forgery");
        foreach (var level in new string?[] { "intermediate", null })
        {
            factory.Learning.Level = level;
            using var response = await PatchAsync(client, factory.Learning.CourseId, level);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(level, factory.Learning.Payload!.Value.GetProperty("level").GetString());
            Assert.Equal("level-intent", factory.Learning.Key); Assert.Equal("server-learning-token", factory.Learning.Token);
            Assert.Equal("Validated teacher", factory.Learning.ActorName);
            var detail = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.Equal(level, detail.GetProperty("level").GetString()); Assert.Equal(JsonValueKind.Null, detail.GetProperty("currentLevel").ValueKind);
            Assert.True(detail.TryGetProperty("draftRevision", out _)); Assert.True(detail.TryGetProperty("modules", out _));
        }
    }

    [Fact(DisplayName = nameof(FieldInvalidAndFieldErrorsReachBrowserUnchanged))]
    public async Task FieldInvalidAndFieldErrorsReachBrowserUnchanged()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        factory.Learning.Status = HttpStatusCode.UnprocessableEntity; factory.Learning.ProblemCode = "FIELD_INVALID";
        factory.Learning.ProblemErrors = JsonSerializer.SerializeToElement(new { level = new[] { "Invalid field: level." } });
        using var response = await PatchAsync(client, factory.Learning.CourseId, "expert");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("FIELD_INVALID", problem.GetProperty("code").GetString());
        Assert.Equal("Invalid field: level.", problem.GetProperty("errors").GetProperty("level")[0].GetString());
    }

    [Fact(DisplayName = nameof(UpstreamForbiddenCodeSurvivesProxyAndReaderCannotReachLearning))]
    public async Task UpstreamForbiddenCodeSurvivesProxyAndReaderCannotReachLearning()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        factory.Learning.Status = HttpStatusCode.Forbidden; factory.Learning.ProblemCode = "PERMISSION_DENIED";
        using var forbidden = await PatchAsync(client, factory.Learning.CourseId, "advanced");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("PERMISSION_DENIED", (await forbidden.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        var calls = factory.Learning.Calls; factory.Identity.Permissions = ["autoria.ler"];
        using var reader = await PatchAsync(client, factory.Learning.CourseId, "beginner");
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode); Assert.Equal(calls, factory.Learning.Calls);
    }

    private static async Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid courseId, string? level)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/courses/{courseId}") { Content = JsonContent.Create(new { level }) };
        request.Headers.Add("Idempotency-Key", "level-intent"); return await client.SendAsync(request, Cancellation);
    }
}
