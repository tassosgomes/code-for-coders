using System.Net;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class PlaybackDeliveryTests(VideoLibraryApiFactory factory)
{
    [Theory(DisplayName = nameof(SessionResourcesRejectUnknownExpiredAndOtherStudentSessions))]
    [InlineData("playlist", "unknown", 404)]
    [InlineData("variants/480p", "unknown", 404)]
    [InlineData("key", "unknown", 404)]
    [InlineData("playlist", "expired", 410)]
    [InlineData("variants/480p", "expired", 410)]
    [InlineData("key", "expired", 410)]
    [InlineData("playlist", "other", 404)]
    [InlineData("variants/480p", "other", 404)]
    [InlineData("key", "other", 404)]
    public async Task SessionResourcesRejectUnknownExpiredAndOtherStudentSessions(string resource, string scenario, int expected)
    {
        var ct = TestContext.Current.CancellationToken; var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, ct); using var opener = test.Client();
        var session = await test.OpenAsync(opener, ct);
        if (scenario == "expired") await test.ExpireAsync(session, ct);
        using var reader = test.Client(scenario == "other" ? Guid.CreateVersion7() : test.Student);
        using var response = await reader.GetAsync($"/internal/v1/playback-sessions/{(scenario == "unknown" ? Guid.CreateVersion7() : session):D}/{resource}", ct);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact(DisplayName = nameof(TwoStudentsReadSameRealS3ObjectsAndDecryptedVideoKey))]
    public async Task TwoStudentsReadSameRealS3ObjectsAndDecryptedVideoKey()
    {
        var ct = TestContext.Current.CancellationToken; var test = new PlaybackTestContext(factory);
        await test.SeedAsync(true, ct); using var first = test.Client(); using var second = test.Client(Guid.CreateVersion7());
        var firstSession = await test.OpenAsync(first, ct); var secondSession = await test.OpenAsync(second, ct);
        foreach (var resource in new[] { "playlist", "variants/480p", "key" })
        {
            using var a = await first.GetAsync($"/internal/v1/playback-sessions/{firstSession:D}/{resource}", ct);
            using var b = await second.GetAsync($"/internal/v1/playback-sessions/{secondSession:D}/{resource}", ct);
            Assert.Equal(HttpStatusCode.OK, a.StatusCode); Assert.Equal(HttpStatusCode.OK, b.StatusCode);
            Assert.True(a.Headers.CacheControl!.NoStore); Assert.True(b.Headers.CacheControl!.NoStore);
            var bytes = await a.Content.ReadAsByteArrayAsync(ct); Assert.Equal(bytes, await b.Content.ReadAsByteArrayAsync(ct));
            if (resource == "key") Assert.Equal(test.VideoKey, bytes);
            else
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                Assert.DoesNotContain("c4c-key:", text);
                Assert.DoesNotContain("st=", text);
                if (resource == "playlist") Assert.Contains("variants/480p", text);
                else { Assert.Contains("../key", text); Assert.Contains(test.Prefix + "480p/segment_00001.ts", text); }
            }
        }
    }

    [Fact(DisplayName = nameof(AnonymousAndForeignAudienceCannotReadPlayback))]
    public async Task AnonymousAndForeignAudienceCannotReadPlayback()
    {
        var ct = TestContext.Current.CancellationToken; var test = new PlaybackTestContext(factory);
        using var anonymous = factory.CreateClient();
        using var response = await anonymous.GetAsync($"/internal/v1/playback-sessions/{Guid.CreateVersion7():D}/key", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var foreign = test.Client(audience: "learning");
        using var rejected = await foreign.GetAsync($"/internal/v1/playback-sessions/{Guid.CreateVersion7():D}/key", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }
}
