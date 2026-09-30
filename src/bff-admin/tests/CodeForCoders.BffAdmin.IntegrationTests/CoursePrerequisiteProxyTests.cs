using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CoursePrerequisiteProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealClientForwardsEncodedTitleTogetherWithPublishedStatus))]
    public async Task RealClientForwardsEncodedTitleTogetherWithPublishedStatus()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        Assert.IsType<CourseAuthoringClient>(factory.Services.GetRequiredService<ICourseAuthoringClient>());
        const string title = "C# básico & APIs";
        using var response = await client.GetAsync($"/api/v1/courses?status=published&title={Uri.EscapeDataString(title)}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("status=published", factory.Learning.Uri!.Query);
        Assert.Contains($"title={Uri.EscapeDataString(title)}", factory.Learning.Uri.Query);
        Assert.Equal(1, factory.Learning.Calls);
    }

    [Fact(DisplayName = nameof(InvalidTitleLengthIsRejectedBeforeCallingLearning))]
    public async Task InvalidTitleLengthIsRejectedBeforeCallingLearning()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        foreach (var title in new[] { "", "a", new string('x', 101) })
        {
            using var response = await client.GetAsync($"/api/v1/courses?title={title}", Cancellation);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("INVALID_REQUEST", (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString());
        }
        Assert.Equal(0, factory.Learning.Calls);
    }

    [Fact(DisplayName = nameof(ReadAndPatchPreserveTextOrderedRecommendationsAndIntentHeaders))]
    public async Task ReadAndPatchPreserveTextOrderedRecommendationsAndIntentHeaders()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        var first = Guid.CreateVersion7(); var second = Guid.CreateVersion7();
        factory.Learning.Prerequisite = new("Git basics", [new(first, "First current title"), new(second, "Second current title")]);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal("Git basics", detail.GetProperty("prerequisite").GetProperty("text").GetString());
        Assert.Equal(first, detail.GetProperty("prerequisite").GetProperty("recommendedCourses")[0].GetProperty("courseId").GetGuid());
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/courses/{factory.Learning.CourseId}")
        { Content = JsonContent.Create(new { prerequisiteText = "Git basics", recommendedCourseIds = new[] { first, second } }) };
        request.Headers.Add("Idempotency-Key", "prerequisite-intent");
        using var response = await client.SendAsync(request, Cancellation); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Git basics", factory.Learning.Payload!.Value.GetProperty("prerequisiteText").GetString());
        Assert.Equal(second, factory.Learning.Payload.Value.GetProperty("recommendedCourseIds")[1].GetGuid());
        Assert.Equal("prerequisite-intent", factory.Learning.Key); Assert.Equal("server-learning-token", factory.Learning.Token);
        Assert.Equal("Validated teacher", factory.Learning.ActorName);
        var changed = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(detail.GetProperty("prerequisite").GetRawText(), changed.GetProperty("prerequisite").GetRawText());
    }

    [Fact(DisplayName = nameof(RecommendedCourseInvalidPreservesCodeAndIndexedErrors))]
    public async Task RecommendedCourseInvalidPreservesCodeAndIndexedErrors()
    {
        await using var factory = new CourseBffApiFactory(); using var client = await factory.AuthenticatedAsync();
        factory.Learning.Status = HttpStatusCode.UnprocessableEntity; factory.Learning.ProblemCode = "RECOMMENDED_COURSE_INVALID";
        factory.Learning.ProblemErrors = JsonSerializer.SerializeToElement(new Dictionary<string, string[]> { ["recommendedCourseIds[1]"] = ["Invalid recommendation."] });
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/courses/{factory.Learning.CourseId}")
        { Content = JsonContent.Create(new { recommendedCourseIds = new[] { Guid.CreateVersion7() } }) };
        request.Headers.Add("Idempotency-Key", "invalid-recommendation"); using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("RECOMMENDED_COURSE_INVALID", problem.GetProperty("code").GetString());
        Assert.Equal("Invalid recommendation.", problem.GetProperty("errors").GetProperty("recommendedCourseIds[1]")[0].GetString());
    }
}
