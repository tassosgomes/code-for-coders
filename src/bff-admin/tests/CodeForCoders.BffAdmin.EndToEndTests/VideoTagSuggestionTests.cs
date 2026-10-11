using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
[Trait("EndToEnd", "Video tags - Suggestions")]
public sealed class VideoTagSuggestionTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(ForwardsEncodedQueryAndAudienceTokenThroughRealClient))]
    public async Task ForwardsEncodedQueryAndAudienceTokenThroughRealClient()
    {
        var (client, cookie, _) = await TeacherAsync();
        factory.VideoLibraryHandler.Tags = ["Introdução", "Docker Compose"];
        using (client)
        using (var request = GetRequest(cookie, "Dóck%_&"))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpMethod.Get, factory.VideoLibraryHandler.LastMethod);
            Assert.Equal("/internal/v1/video-tags?q=D%C3%B3ck%25_%26", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("teacher-media-token", factory.VideoLibraryHandler.LastAccessToken);
            Assert.Equal("media", factory.StaffSessionIdentityHandler.LastValidationAudience);
            var tags = await response.Content.ReadFromJsonAsync<VideoTagListResponse>(TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "Introdução", "Docker Compose" }, tags!.Data);
        }
    }

    [Fact(DisplayName = nameof(OmittedQueryReturnsEmptyListWithoutPagination))]
    public async Task OmittedQueryReturnsEmptyListWithoutPagination()
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client) using (var request = GetRequest(cookie))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/internal/v1/video-tags", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("{\"data\":[]}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact(DisplayName = nameof(ChecksSessionAndPermissionBeforeCallingMedia))]
    public async Task ChecksSessionAndPermissionBeforeCallingMedia()
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client)
        {
            using var anonymousClient = factory.CreateClient();
            using var anonymous = await anonymousClient.GetAsync("/api/v1/video-tags", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession! with { Permissions = [] };
            using var request = GetRequest(cookie);
            using var denied = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvw")]
    public async Task InvalidQueryDoesNotReachMedia(string query)
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client) using (var request = GetRequest(cookie, query))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("INVALID_REQUEST", json.RootElement.GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData(400, "INVALID_REQUEST", 400)]
    [InlineData(401, "TOKEN_INVALID", 401)]
    [InlineData(403, "PERMISSION_DENIED", 403)]
    [InlineData(503, "MEDIA_UNAVAILABLE", 502)]
    [InlineData(504, "MEDIA_UNAVAILABLE", 504)]
    public async Task PreservesExpectedMediaErrors(int upstream, string code, int expected)
    {
        var (client, cookie, _) = await TeacherAsync();
        factory.VideoLibraryHandler.StatusCode = (HttpStatusCode)upstream;
        factory.VideoLibraryHandler.ProblemCode = code;
        using (client) using (var request = GetRequest(cookie, "dock"))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(expected, (int)response.StatusCode);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
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
        loginRequest.Headers.Add("Idempotency-Key", "video-title-login");
        using var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = await loginResponse.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            session.AccountId, "Marina Alves", ["professor"], ["midia.enviar"], DateTimeOffset.UtcNow.AddMinutes(30), "teacher-media-token");
        return (client, loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0], session.CsrfToken);
    }


    private static HttpRequestMessage GetRequest(string cookie, string? query = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/video-tags" + (query is null ? "" : $"?q={Uri.EscapeDataString(query)}"));
        request.Headers.Add("Cookie", cookie);
        return request;
    }
}
