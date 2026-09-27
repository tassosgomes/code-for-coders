using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoLibraryTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoLibrary_RefusesActorWithoutPermissionBeforeCallingMedia))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_RefusesActorWithoutPermissionBeforeCallingMedia()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["suporte"],
            ["suporte.atender"]);

        using var response = await GetVideosAsync(client, login.Cookie);
        var code = await ReadCodeAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", code);
        Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        Assert.Null(factory.StaffSessionIdentityHandler.LastValidationAudience);
    }

    [Fact(DisplayName = nameof(VideoLibrary_RequestsMediaAudienceAndReturnsEmptyPageForTeacher))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_RequestsMediaAudienceAndReturnsEmptyPageForTeacher()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["professor"],
            ["autoria.ler", "midia.enviar"],
            "teacher-media-token");

        using var response = await GetVideosAsync(client, login.Cookie);
        var page = await response.Content.ReadFromJsonAsync<VideoPageResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(page!.Data);
        Assert.Equal(1, page.Pagination.Page);
        Assert.Equal(10, page.Pagination.Size);
        Assert.Equal("media", factory.StaffSessionIdentityHandler.LastValidationAudience);
        Assert.Equal("teacher-media-token", factory.VideoLibraryHandler.LastAccessToken);
        Assert.Equal("/internal/v1/videos?_page=1&_size=10", factory.VideoLibraryHandler.LastPath);
        Assert.Equal(1, factory.VideoLibraryHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(VideoLibrary_MapsMediaNotFoundToVideoNotFound))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_MapsMediaNotFoundToVideoNotFound()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["professor"],
            ["midia.enviar"],
            "teacher-media-token");
        factory.VideoLibraryHandler.StatusCode = HttpStatusCode.NotFound;
        factory.VideoLibraryHandler.ProblemCode = "VIDEO_NOT_FOUND";
        var videoId = Guid.CreateVersion7();

        using var response = await GetVideoAsync(client, login.Cookie, videoId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("VIDEO_NOT_FOUND", await ReadCodeAsync(response));
        Assert.Equal($"/internal/v1/videos/{videoId:D}", factory.VideoLibraryHandler.LastPath);
    }

    [Fact(DisplayName = nameof(VideoLibrary_MapsMediaServerErrorsToBadGateway))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_MapsMediaServerErrorsToBadGateway()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["professor"],
            ["midia.enviar"],
            "teacher-media-token");
        factory.VideoLibraryHandler.StatusCode = HttpStatusCode.ServiceUnavailable;

        using var response = await GetVideosAsync(client, login.Cookie);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MEDIA_UNAVAILABLE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_MapsMediaTimeoutToGatewayTimeout))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_MapsMediaTimeoutToGatewayTimeout()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["professor"],
            ["midia.enviar"],
            "teacher-media-token");
        factory.VideoLibraryHandler.StatusCode = HttpStatusCode.GatewayTimeout;

        using var response = await GetVideosAsync(client, login.Cookie);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("MEDIA_UNAVAILABLE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_MapsHttpClientTimeoutToGatewayTimeout))]
    [Trait("Layer", "BffAdmin video library - EndToEnd")]
    public async Task VideoLibrary_MapsHttpClientTimeoutToGatewayTimeout()
    {
        using var httpClient = new HttpClient(new TimeoutHttpHandler())
        {
            BaseAddress = new Uri("http://media.test/", UriKind.Absolute),
        };
        var mediaClient = new VideoLibraryClient(httpClient);

        var result = await mediaClient.ListVideosAsync(
            1,
            10,
            [],
            null,
            "teacher-media-token",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, result.StatusCode);
        Assert.Equal("MEDIA_UNAVAILABLE", result.Code);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.VideoLibraryHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "video-library-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static Task<HttpResponseMessage> GetVideosAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/videos");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> GetVideoAsync(HttpClient client, string cookie, Guid videoId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/videos/{videoId:D}");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static StaffSessionValidatedV1 ValidatedSession(
        Guid accountId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        string? accessToken = null)
        => new(accountId, "Marina Alves", roles, permissions, DateTimeOffset.UtcNow.AddMinutes(30), accessToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);

    private sealed class TimeoutHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new TaskCanceledException("Media request timed out.");
    }
}
