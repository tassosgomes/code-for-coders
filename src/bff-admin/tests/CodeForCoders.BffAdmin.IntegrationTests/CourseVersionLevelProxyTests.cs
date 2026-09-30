using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseVersionLevelProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealLearningClientPreservesPublishedLevelTextAndHistoricalTitlesInOrder))]
    public async Task RealLearningClientPreservesPublishedLevelTextAndHistoricalTitlesInOrder()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        Assert.IsType<CourseAuthoringClient>(factory.Services.GetRequiredService<ICourseAuthoringClient>());
        var first = Guid.CreateVersion7(); var second = Guid.CreateVersion7();
        factory.Learning.Level = "advanced";
        factory.Learning.Prerequisite = new("Git basics", [new(second, "Second historical title"), new(first, "First historical title")]);
        using var response = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}/versions/1", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var version = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("advanced", version.GetProperty("level").GetString());
        Assert.Equal("Git basics", version.GetProperty("prerequisite").GetProperty("text").GetString());
        var references = version.GetProperty("prerequisite").GetProperty("recommendedCourses");
        Assert.Equal(second, references[0].GetProperty("courseId").GetGuid()); Assert.Equal(first, references[1].GetProperty("courseId").GetGuid());
        Assert.Equal("Second historical title", references[0].GetProperty("title").GetString());
        Assert.Equal("server-learning-token", factory.Learning.Token); Assert.EndsWith("/versions/1", factory.Learning.Uri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(LegacyVersionPreservesExplicitNullLevelAndEmptyPrerequisite))]
    public async Task LegacyVersionPreservesExplicitNullLevelAndEmptyPrerequisite()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        var version = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}/versions/1", Cancellation);
        Assert.Equal(JsonValueKind.Null, version.GetProperty("level").ValueKind);
        Assert.Equal(JsonValueKind.Null, version.GetProperty("prerequisite").GetProperty("text").ValueKind);
        Assert.Equal(0, version.GetProperty("prerequisite").GetProperty("recommendedCourses").GetArrayLength());
    }
}
