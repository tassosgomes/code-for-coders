using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
[Trait("Layer", "Video tags - EndToEnd")]
public sealed class VideoTagFilterTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(ForwardsRepeatedTagsModeAndExistingFiltersEscaped))]
    public async Task ForwardsRepeatedTagsModeAndExistingFiltersEscaped()
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client)
        using (var response = await ListAsync(client, cookie, "tag=docker&tag=m%C3%B3dulo%202&tagMatch=any&status=failed&q=Inje%C3%A7%C3%A3o&_page=2&_size=1"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/internal/v1/videos?_page=2&_size=1&status=failed&q=Inje%C3%A7%C3%A3o&tag=docker&tag=m%C3%B3dulo%202&tagMatch=any", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("teacher-media-token", factory.VideoLibraryHandler.LastAccessToken);
        }
    }

    [Fact(DisplayName = nameof(DefaultModeIsAllAndUnknownTagIsForwarded))]
    public async Task DefaultModeIsAllAndUnknownTagIsForwarded()
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client)
        using (var response = await ListAsync(client, cookie, "tag=unknown"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/internal/v1/videos?_page=1&_size=10&tag=unknown&tagMatch=all", factory.VideoLibraryHandler.LastPath);
        }
    }

    [Theory]
    [InlineData("tagMatch=invalid")]
    [InlineData("tagMatch=")]
    [InlineData("tag=")]
    [InlineData("long-tag")]
    [InlineData("too-many")]
    public async Task RejectsInvalidFilterBeforeCallingMedia(string query)
    {
        if (query == "long-tag") query = "tag=" + new string('a', 101);
        if (query == "too-many") query = string.Join('&', Enumerable.Repeat("tag=docker", 11));
        var (client, cookie, _) = await TeacherAsync();
        using (client)
        using (var response = await ListAsync(client, cookie, query))
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("INVALID_REQUEST", json.RootElement.GetProperty("code").GetString());
            Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        }
    }

    private static async Task<HttpResponseMessage> ListAsync(HttpClient client, string cookie, string query)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/videos?" + query);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(HttpClient Client, string Cookie, string Csrf)> TeacherAsync()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.VideoLibraryHandler.Reset();
        var client = factory.CreateClient();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        loginRequest.Headers.Add("Idempotency-Key", "video-tag-filter-login");
        using var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = await loginResponse.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            session.AccountId, "Marina Alves", ["professor"], ["midia.enviar"], DateTimeOffset.UtcNow.AddMinutes(30), "teacher-media-token");
        return (client, loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0], session.CsrfToken);
    }

}
