using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoUploadResumeTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoUploadResume_ForwardsPendingListPageAndAccessToken))]
    [Trait("Layer", "BffAdmin video upload resume - EndToEnd")]
    public async Task VideoUploadResume_ForwardsPendingListPageAndAccessToken()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-resume-token");
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/video-uploads?_page=2&_size=25", login);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("teacher-resume-token", factory.VideoUploadHandler.LastAccessToken);
        Assert.Equal("/internal/v1/video-uploads?_page=2&_size=25", factory.VideoUploadHandler.LastPath);
    }

    [Fact(DisplayName = nameof(VideoUploadResume_MapsUnavailablePendingListToBadGateway))]
    [Trait("Layer", "BffAdmin video upload resume - EndToEnd")]
    public async Task VideoUploadResume_MapsUnavailablePendingListToBadGateway()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-resume-token");
        factory.VideoUploadHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        factory.VideoUploadHandler.ProblemCode = "STORAGE_UNAVAILABLE";
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/video-uploads", login);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MEDIA_UNAVAILABLE", await ReadCodeAsync(response));
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.VideoUploadHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "video-upload-resume-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, LoginResult login)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        return request;
    }

    private static StaffSessionValidatedV1 TeacherSession(Guid accountId, string accessToken)
        => new(
            accountId,
            "Marina Alves",
            ["professor"],
            ["midia.enviar"],
            DateTimeOffset.UtcNow.AddMinutes(30),
            accessToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
