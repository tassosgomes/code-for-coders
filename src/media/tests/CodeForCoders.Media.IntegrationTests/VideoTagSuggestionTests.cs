using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
[Trait("Integration", "Video tags - Suggestions")]
public sealed class VideoTagSuggestionTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(NormalizesQueryAndReturnsCanonicalSpelling))]
    public async Task NormalizesQueryAndReturnsCanonicalSpelling()
    {
        var video = await SeedAsync(["Introdução", "Docker"]);
        Assert.Equal(new[] { "Introdução" }, await SuggestionsAsync(video.TenantId, "INTRODUCAO"));
        Assert.Empty(await SuggestionsAsync(video.TenantId, "absent"));
    }

    [Fact(DisplayName = nameof(PrefixesPrecedeOtherMatchesInAlphabeticalOrder))]
    public async Task PrefixesPrecedeOtherMatchesInAlphabeticalOrder()
    {
        var video = await SeedAsync(["Moby Docker", "Docker Compose", "Docker", "A Docker guide"]);
        Assert.Equal(new[] { "Docker", "Docker Compose", "A Docker guide", "Moby Docker" }, await SuggestionsAsync(video.TenantId, "dock"));
    }

    [Fact(DisplayName = nameof(LastUnlinkRemovesSuggestionButKeepsCatalogSpelling))]
    public async Task LastUnlinkRemovesSuggestionButKeepsCatalogSpelling()
    {
        var first = await SeedAsync(["Docker"]);
        var second = await SeedAsync(["docker"], first.TenantId);
        await ReplaceAsync(first, []);
        Assert.Equal(new[] { "Docker" }, await SuggestionsAsync(first.TenantId));
        await ReplaceAsync(second, []);
        Assert.Empty(await SuggestionsAsync(first.TenantId));
        await ReplaceAsync(second, ["DOCKER"]);
        Assert.Equal(new[] { "Docker" }, await SuggestionsAsync(first.TenantId));
    }

    [Fact(DisplayName = nameof(OtherSchoolsNeverAppear))]
    public async Task OtherSchoolsNeverAppear()
    {
        var local = await SeedAsync(["Docker"]);
        await SeedAsync(["Secret Docker"]);
        Assert.Equal(new[] { "Docker" }, await SuggestionsAsync(local.TenantId, "dock"));
        Assert.Empty(await SuggestionsAsync(Guid.CreateVersion7()));
    }

    [Fact(DisplayName = nameof(DefaultAndFilteredSuggestionsAreCappedAtTwenty))]
    public async Task DefaultAndFilteredSuggestionsAreCappedAtTwenty()
    {
        var tenant = Guid.CreateVersion7();
        for (var batch = 2; batch >= 0; batch--)
            await SeedAsync(Enumerable.Range(batch * 10, 10).Select(index => $"Tag {index:D2}").ToArray(), tenant);
        var expected = Enumerable.Range(0, 20).Select(index => $"Tag {index:D2}").ToArray();
        Assert.Equal(expected, await SuggestionsAsync(tenant));
        Assert.Equal(expected, await SuggestionsAsync(tenant, "tag"));
    }

    [Fact(DisplayName = nameof(PercentUnderscoreAndBackslashAreLiteral))]
    public async Task PercentUnderscoreAndBackslashAreLiteral()
    {
        var video = await SeedAsync(["Docker", "Dock%", "Dock_", "Dock\\path"]);
        Assert.Equal(new[] { "Dock%" }, await SuggestionsAsync(video.TenantId, "dock%"));
        Assert.Equal(new[] { "Dock_" }, await SuggestionsAsync(video.TenantId, "dock_"));
        Assert.Equal(new[] { "Dock\\path" }, await SuggestionsAsync(video.TenantId, "dock\\"));
    }

    [Fact(DisplayName = nameof(AllVideoStatesContributeSuggestions))]
    public async Task AllVideoStatesContributeSuggestions()
    {
        var tenant = Guid.CreateVersion7();
        await SeedAsync(["Received"], tenant);
        foreach (var state in new[] { "Preparing", "Ready", "Failed" })
        {
            var video = await SeedAsync([state], tenant);
            await using var scope = Scope(tenant);
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var current = await db.Videos.FindAsync([video.VideoId], TestContext.Current.CancellationToken);
            var lease = Guid.CreateVersion7();
            current!.MarkPreparing(lease, DateTimeOffset.UtcNow.AddMinutes(5));
            if (state == "Ready") current.MarkReady(lease, 10, 1024, new byte[32], "test-key");
            if (state == "Failed") current.MarkFailed(lease, VideoFailureReasons.UnreadableFile);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal(new[] { "Failed", "Preparing", "Ready", "Received" }, await SuggestionsAsync(tenant));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvw")]
    public async Task InvalidQueryReturnsInvalidRequest(string query)
    {
        using var client = factory.CreateClient();
        using var request = Request(Guid.CreateVersion7(), query);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadAsync(response);
        Assert.Equal("INVALID_REQUEST", json.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(PermissionAndTokenAreRequired))]
    public async Task PermissionAndTokenAreRequired()
    {
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/internal/v1/video-tags", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = Request(Guid.CreateVersion7());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(Guid.CreateVersion7()));
        using var denied = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private async Task<Video> SeedAsync(string[] tags, Guid? tenant = null)
    {
        var video = Video.Create(tenant ?? Guid.CreateVersion7(), "Original", Guid.CreateVersion7(), "Teacher", DateTimeOffset.UtcNow);
        await using (var scope = Scope(video.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            db.Videos.Add(video);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await ReplaceAsync(video, tags);
        return video;
    }

    private AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        return scope;
    }

    private async Task ReplaceAsync(Video video, string[] tags)
    {
        using var client = factory.CreateClient();
        using var request = Request(video.TenantId);
        request.Method = HttpMethod.Put;
        request.RequestUri = new Uri($"/internal/v1/videos/{video.VideoId}/tags", UriKind.Relative);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        request.Content = JsonContent.Create(new { tags });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpRequestMessage Request(Guid tenant, string? query = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/video-tags" + (query is null ? "" : $"?q={Uri.EscapeDataString(query)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(tenant, Guid.CreateVersion7(), "midia.enviar"));
        return request;
    }

    private async Task<string[]> SuggestionsAsync(Guid tenant, string? query = null)
    {
        using var client = factory.CreateClient();
        using var request = Request(tenant, query);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadAsync(response);
        return json.RootElement.GetProperty("data").EnumerateArray().Select(tag => tag.GetString()!).ToArray();
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
}
