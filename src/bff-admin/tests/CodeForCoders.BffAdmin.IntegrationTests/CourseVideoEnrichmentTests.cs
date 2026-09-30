using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseVideoEnrichmentTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static JsonElement Video(JsonElement course) => course.GetProperty("modules")[0].GetProperty("lessons")[0].GetProperty("video");

    [Fact(DisplayName = nameof(ReadySelectorUsesRealMediaClientAndRequiresMediaPermission))]
    public async Task ReadySelectorUsesRealMediaClientAndRequiresMediaPermission()
    {
        await using var factory = new CourseBffApiFactory();
        factory.Identity.Permissions = ["autoria.ler", "autoria.editar", "midia.enviar"];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync("/api/v1/videos?status=ready&_page=1&_size=20", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("ready", page.GetProperty("data")[0].GetProperty("status").GetString());
        Assert.Equal("School colleague", page.GetProperty("data")[0].GetProperty("uploadedBy").GetProperty("name").GetString());
        Assert.All(factory.Media.Requests, request =>
        {
            Assert.Contains("status=ready", request.Uri.Query, StringComparison.Ordinal);
            Assert.Equal("server-media-token", request.Token);
        });
        factory.Identity.Permissions = ["autoria.ler", "autoria.editar"];
        using var forbidden = await client.GetAsync("/api/v1/videos?status=ready", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); Assert.Single(factory.Media.Requests);
    }

    [Fact(DisplayName = nameof(RealClientAndHostReadCurrentTitleAndDurationWithMediaAudienceToken))]
    public async Task RealClientAndHostReadCurrentTitleAndDurationWithMediaAudienceToken()
    {
        await using var factory = new CourseBffApiFactory(); var id = Guid.CreateVersion7(); SetVideos(factory, [id]);
        using var client = await factory.AuthenticatedAsync();
        var first = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal("Current Media title", Video(first).GetProperty("title").GetString());
        Assert.Equal(125, Video(first).GetProperty("durationSeconds").GetInt32());
        factory.Media.Title = "Renamed in Media";
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal("Renamed in Media", Video(second).GetProperty("title").GetString());
        Assert.All(factory.Media.Requests, request => Assert.Equal("server-media-token", request.Token));
        Assert.Equal("server-learning-token", factory.Learning.Token);
    }

    [Theory(DisplayName = nameof(MediaFailureOrNonReadyMetadataKeepsCourse200AndVideoReference))]
    [InlineData("outage")]
    [InlineData("malformed")]
    [InlineData("preparing")]
    [InlineData("failed")]
    public async Task MediaFailureOrNonReadyMetadataKeepsCourse200AndVideoReference(string failure)
    {
        await using var factory = new CourseBffApiFactory(); var id = Guid.CreateVersion7(); SetVideos(factory, [id]);
        factory.Media.Unavailable = failure == "outage"; factory.Media.Malformed = failure == "malformed";
        if (failure is "preparing" or "failed") factory.Media.Status = failure;
        using var client = await factory.AuthenticatedAsync(); using var response = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var video = Video(await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation));
        Assert.Equal(id, video.GetProperty("videoId").GetGuid()); Assert.Single(video.EnumerateObject());
    }

    [Fact(DisplayName = nameof(DuplicateIdsAreFetchedOnceAndConcurrencyIsBounded))]
    public async Task DuplicateIdsAreFetchedOnceAndConcurrencyIsBounded()
    {
        await using var factory = new CourseBffApiFactory(); var ids = Enumerable.Range(0, 12).Select(_ => Guid.CreateVersion7()).ToArray();
        SetVideos(factory, ids.Concat(ids).ToArray());
        using var client = await factory.AuthenticatedAsync();
        var course = await client.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal(24, course.GetProperty("modules")[0].GetProperty("lessons").GetArrayLength());
        Assert.Equal(12, factory.Media.Requests.Count); Assert.InRange(factory.Media.MaxConcurrency, 1, 4);
    }

    [Fact(DisplayName = nameof(SlowMediaHasTotalBudgetAndStillReturnsCourse))]
    public async Task SlowMediaHasTotalBudgetAndStillReturnsCourse()
    {
        await using var factory = new CourseBffApiFactory(); var id = Guid.CreateVersion7(); SetVideos(factory, [id]);
        factory.Media.Delay = TimeSpan.FromSeconds(30);
        using var client = await factory.AuthenticatedAsync(); var watch = Stopwatch.StartNew();
        using var response = await client.GetAsync($"/api/v1/courses/{factory.Learning.CourseId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6));
        Assert.Single(Video(await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).EnumerateObject());
    }

    private static void SetVideos(CourseBffApiFactory factory, Guid[] ids)
        => factory.Learning.Modules = [JsonSerializer.SerializeToElement(new { moduleId = Guid.CreateVersion7(), title = "Module", position = 1,
            lessons = ids.Select((id, index) => new { lessonId = Guid.CreateVersion7(), title = "Lesson", position = index + 1, video = new { videoId = id } }) })];
}
