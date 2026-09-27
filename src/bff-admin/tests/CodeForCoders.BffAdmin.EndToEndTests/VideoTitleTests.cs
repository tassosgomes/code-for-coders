using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoTitleTests(BffAdminApiFactory factory)
{
    [Fact]
    public async Task ListForwardsRepeatedStatusAndAccentSearchToMedia()
    {
        var (client, cookie, _) = await TeacherAsync();
        using (client)
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/videos?status=received&status=failed&q=injecao"))
        {
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/internal/v1/videos?_page=1&_size=10&status=received&status=failed&q=injecao", factory.VideoLibraryHandler.LastPath);
        }
    }

    [Fact]
    public async Task TeacherCanPatchColleagueVideoThroughMedia()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        using (client)
        using (var request = PatchRequest(Guid.CreateVersion7(), cookie, csrf, "Aula corrigida"))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpMethod.Patch, factory.VideoLibraryHandler.LastMethod);
            Assert.Equal("edit-video-title", factory.VideoLibraryHandler.LastIdempotencyKey);
            Assert.Equal("media", factory.StaffSessionIdentityHandler.LastValidationAudience);
        }
    }

    [Fact]
    public async Task TitleRequiredFromMediaRemainsUnprocessable()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        factory.VideoLibraryHandler.StatusCode = HttpStatusCode.UnprocessableEntity;
        factory.VideoLibraryHandler.ProblemCode = "TITLE_REQUIRED";
        using (client)
        using (var request = PatchRequest(Guid.CreateVersion7(), cookie, csrf, " "))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("TITLE_REQUIRED", document.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task MediaUnavailableDuringRenameReturnsBadGateway()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        factory.VideoLibraryHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        using (client)
        using (var request = PatchRequest(Guid.CreateVersion7(), cookie, csrf, "Aula corrigida"))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal("MEDIA_UNAVAILABLE", document.RootElement.GetProperty("code").GetString());
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

    private static HttpRequestMessage PatchRequest(Guid videoId, string cookie, string csrf, string title)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/videos/{videoId:D}")
        {
            Content = JsonContent.Create(new { title }),
        };
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("X-CSRF-Token", csrf);
        request.Headers.Add("Idempotency-Key", "edit-video-title");
        return request;
    }
}
