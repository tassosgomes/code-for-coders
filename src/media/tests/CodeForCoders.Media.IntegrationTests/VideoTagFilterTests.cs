using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.ValueObjects;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
[Trait("Integration", "Video tags - Filters")]
public sealed class VideoTagFilterTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(DefaultAllReturnsOnlyVideoWithBothTags))]
    public async Task DefaultAllReturnsOnlyVideoWithBothTags()
    {
        var (tenant, first, _, _) = await SeedExamplesAsync();
        using var page = await ListAsync(tenant, "tag=docker&tag=m%C3%B3dulo%202");
        Assert.Equal(new[] { first.VideoId }, Ids(page));
        Assert.Equal(1, Total(page));
        Assert.Equal(new[] { "Docker", "Módulo 2" }, page.RootElement.GetProperty("data")[0].GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
    }

    [Fact(DisplayName = nameof(AnyReturnsVideosWithEitherTag))]
    public async Task AnyReturnsVideosWithEitherTag()
    {
        var (tenant, first, second, third) = await SeedExamplesAsync();
        using var page = await ListAsync(tenant, "tag=docker&tag=m%C3%B3dulo%202&tagMatch=any");
        Assert.Equal(new[] { first.VideoId, second.VideoId, third.VideoId }.Order(), Ids(page).Order());
        Assert.Equal(3, Total(page));
    }

    [Fact(DisplayName = nameof(UnknownTagMakesAllEmpty))]
    public async Task UnknownTagMakesAllEmpty()
    {
        var (tenant, _, _, _) = await SeedExamplesAsync();
        using var page = await ListAsync(tenant, "tag=docker&tag=unknown&tagMatch=all");
        Assert.Empty(Ids(page)); Assert.Equal(0, Total(page));
    }

    [Fact(DisplayName = nameof(UnknownTagDoesNotContributeToAny))]
    public async Task UnknownTagDoesNotContributeToAny()
    {
        var (tenant, first, second, _) = await SeedExamplesAsync();
        using var page = await ListAsync(tenant, "tag=docker&tag=unknown&tagMatch=any");
        Assert.Equal(new[] { first.VideoId, second.VideoId }.Order(), Ids(page).Order());
        Assert.Equal(2, Total(page));
    }

    [Fact(DisplayName = nameof(FilterNormalizesCaseAccentsSpacesAndDuplicates))]
    public async Task FilterNormalizesCaseAccentsSpacesAndDuplicates()
    {
        var (tenant, first, _, _) = await SeedExamplesAsync();
        using var page = await ListAsync(tenant, "tag=%20DOCKER%20&tag=MODULO%20%20%202&tag=m%C3%B3dulo%202");
        Assert.Equal(new[] { first.VideoId }, Ids(page)); Assert.Equal(1, Total(page));
    }

    [Fact(DisplayName = nameof(TagsCombineWithStatusTitleAndPagination))]
    public async Task TagsCombineWithStatusTitleAndPagination()
    {
        var tenant = Guid.CreateVersion7();
        var first = await SeedAsync(tenant, "Injeção A", ["Docker"], failed: true);
        var second = await SeedAsync(tenant, "Injeção B", ["Docker"], failed: true);
        await SeedAsync(tenant, "Injeção sem tag", [], failed: true);
        await SeedAsync(tenant, "Outro título", ["Docker"], failed: true);
        await SeedAsync(tenant, "Injeção pronta", ["Docker"]);
        using var pageOne = await ListAsync(tenant, "tag=docker&status=failed&q=injecao&_size=1");
        using var pageTwo = await ListAsync(tenant, "tag=docker&status=failed&q=injecao&_size=1&_page=2");
        Assert.Equal(2, Total(pageOne)); Assert.Equal(2, Total(pageTwo));
        Assert.Equal(2, pageOne.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { second.VideoId }, Ids(pageOne));
        Assert.Equal(new[] { first.VideoId }, Ids(pageTwo));
    }

    [Fact(DisplayName = nameof(FilterNeverRevealsOtherSchools))]
    public async Task FilterNeverRevealsOtherSchools()
    {
        var tenant = Guid.CreateVersion7();
        await SeedAsync(Guid.CreateVersion7(), "Secret", ["Docker", "Secret"]);
        var local = await SeedAsync(tenant, "Local", ["Docker"]);
        using var all = await ListAsync(tenant, "tag=docker&tag=secret");
        using var any = await ListAsync(tenant, "tag=docker&tag=secret&tagMatch=any");
        Assert.Empty(Ids(all)); Assert.Equal(new[] { local.VideoId }, Ids(any));
    }

    [Fact(DisplayName = nameof(UnlinkedTagNoLongerMatchesAndUnfilteredListKeepsAllVideos))]
    public async Task UnlinkedTagNoLongerMatchesAndUnfilteredListKeepsAllVideos()
    {
        var tenant = Guid.CreateVersion7();
        var video = await SeedAsync(tenant, "Original", ["Docker"]);
        using var client = factory.CreateClient();
        using var request = Request(tenant, $"/internal/v1/videos/{video.VideoId}/tags");
        request.Method = HttpMethod.Put;
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        request.Content = JsonContent.Create(new { tags = Array.Empty<string>() });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var filtered = await ListAsync(tenant, "tag=docker");
        using var unfiltered = await ListAsync(tenant);
        Assert.Empty(Ids(filtered)); Assert.Equal(new[] { video.VideoId }, Ids(unfiltered));
    }

    [Fact(DisplayName = nameof(OneHundredCharacterUnknownTagIsAccepted))]
    public async Task OneHundredCharacterUnknownTagIsAccepted()
    {
        using var page = await ListAsync(Guid.CreateVersion7(), "tag=" + new string('a', 100));
        Assert.Empty(Ids(page));
    }

    [Theory]
    [InlineData("tagMatch=invalid")]
    [InlineData("tagMatch=")]
    [InlineData("tag=")]
    [InlineData("tag=%20")]
    [InlineData("long-tag")]
    [InlineData("too-many")]
    public async Task InvalidFiltersReturnInvalidRequest(string query)
    {
        if (query == "long-tag") query = "tag=" + new string('a', 101);
        if (query == "too-many") query = string.Join('&', Enumerable.Repeat("tag=docker", 11));
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), "/internal/v1/videos?" + query);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var json = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_REQUEST", json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(ListMetricUsesOnlyBoundedFilterAndReadyDimensions))]
    public async Task ListMetricUsesOnlyBoundedFilterAndReadyDimensions()
    {
        var measurements = new ConcurrentBag<Dictionary<string, object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "media.videos.listed") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Assert.Equal(1, value);
            measurements.Add(tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        });
        listener.Start();
        var tenant = Guid.CreateVersion7();
        using var unfiltered = await ListAsync(tenant);
        using var all = await ListAsync(tenant, "tag=docker&status=ready");
        using var any = await ListAsync(tenant, "tag=docker&tagMatch=any&status=ready&status=failed");
        Assert.Equal(3, measurements.Count);
        Assert.All(measurements, dimensions => Assert.Equal(2, dimensions.Count));
        Assert.Contains(measurements, dimensions => Equals(dimensions["tag_filter"], "none") && Equals(dimensions["ready_only"], false));
        Assert.Contains(measurements, dimensions => Equals(dimensions["tag_filter"], "all") && Equals(dimensions["ready_only"], true));
        Assert.Contains(measurements, dimensions => Equals(dimensions["tag_filter"], "any") && Equals(dimensions["ready_only"], false));
    }

    private async Task<(Guid Tenant, Video First, Video Second, Video Third)> SeedExamplesAsync()
    {
        var tenant = Guid.CreateVersion7();
        var first = await SeedAsync(tenant, "A", ["Docker", "Módulo 2"]);
        var second = await SeedAsync(tenant, "B", ["Docker"]);
        var third = await SeedAsync(tenant, "C", ["Módulo 2"]);
        return (tenant, first, second, third);
    }

    private async Task<Video> SeedAsync(Guid tenant, string title, string[] tags, bool failed = false)
    {
        var video = Video.Create(tenant, title, Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        if (failed)
        {
            var lease = Guid.CreateVersion7();
            video.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
            video.MarkFailed(lease, VideoFailureReasons.UnreadableFile);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        db.Videos.Add(video);
        foreach (var value in VideoTagSet.Normalize(tags))
        {
            var tag = await db.Tags.SingleOrDefaultAsync(tag => tag.NormalizedName == value.NormalizedName, TestContext.Current.CancellationToken);
            if (tag is null) { tag = VideoTag.Create(tenant, value, DateTimeOffset.UtcNow); db.Tags.Add(tag); }
            db.VideoTags.Add(VideoTagLink.Create(video.VideoId, tag));
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return video;
    }

    private async Task<JsonDocument> ListAsync(Guid tenant, string query = "")
    {
        using var client = factory.CreateClient();
        using var request = Request(tenant, "/internal/v1/videos?" + query);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private HttpRequestMessage Request(Guid tenant, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(tenant, Guid.CreateVersion7(), "midia.enviar"));
        return request;
    }

    private static Guid[] Ids(JsonDocument page) => page.RootElement.GetProperty("data").EnumerateArray().Select(video => video.GetProperty("videoId").GetGuid()).ToArray();
    private static int Total(JsonDocument page) => page.RootElement.GetProperty("pagination").GetProperty("total").GetInt32();
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
}
