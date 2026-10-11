using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoTagTests(BffAdminApiFactory factory)
{
    [Fact]
    public async Task TeacherReplacesTagsThroughRealMediaClient()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        factory.VideoLibraryHandler.Tags = ["Docker"];
        var id = Guid.CreateVersion7();
        using (client) using (var request = PutRequest(id, cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpMethod.Put, factory.VideoLibraryHandler.LastMethod);
            Assert.Equal($"/internal/v1/videos/{id:D}/tags", factory.VideoLibraryHandler.LastPath);
            Assert.Equal("edit-video-tags", factory.VideoLibraryHandler.LastIdempotencyKey);
            Assert.Equal("teacher-media-token", factory.VideoLibraryHandler.LastAccessToken);
            Assert.Equal("media", factory.StaffSessionIdentityHandler.LastValidationAudience);
            using var forwarded = JsonDocument.Parse(factory.VideoLibraryHandler.LastBody!);
            Assert.Equal("docker", forwarded.RootElement.GetProperty("tags")[0].GetString());
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Docker", json.RootElement.GetProperty("tags")[0].GetString());
        }
    }
    [Theory]
    [InlineData("TAG_INVALID", 422)]
    [InlineData("TAGS_LIMIT_EXCEEDED", 422)]
    [InlineData("IDEMPOTENCY_KEY_REUSED", 422)]
    [InlineData("VIDEO_NOT_FOUND", 404)]
    public async Task MediaCodesArePreserved(string code, int status)
    {
        var (client, cookie, csrf) = await TeacherAsync(); factory.VideoLibraryHandler.StatusCode = (HttpStatusCode)status; factory.VideoLibraryHandler.ProblemCode = code;
        using (client) using (var request = PutRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(status, (int)response.StatusCode);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
    }
    [Fact]
    public async Task MissingCsrfIsRejectedBeforeMedia()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        using (client) using (var request = PutRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            request.Headers.Remove("X-CSRF-Token");
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("CSRF_INVALID", json.RootElement.GetProperty("code").GetString());
        }
    }
    [Fact]
    public async Task MissingIdempotencyKeyIsRejectedBeforeMedia()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        using (client) using (var request = PutRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            request.Headers.Remove("Idempotency-Key"); using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
        }
    }
    [Fact]
    public async Task SessionWithoutPermissionCannotReplaceTags()
    {
        var (client, cookie, csrf) = await TeacherAsync();
        var validated = factory.StaffSessionIdentityHandler.ValidatedSession!;
        factory.StaffSessionIdentityHandler.ValidatedSession = validated with { Permissions = [] };
        using (client) using (var request = PutRequest(Guid.CreateVersion7(), cookie, csrf))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, factory.VideoLibraryHandler.RequestCount);
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

    private static HttpRequestMessage PutRequest(Guid videoId, string cookie, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/videos/{videoId:D}/tags") { Content = JsonContent.Create(new { tags = new[] { "docker" } }) };
        request.Headers.Add("Cookie", cookie); request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("X-CSRF-Token", csrf); request.Headers.Add("Idempotency-Key", "edit-video-tags"); return request;
    }
}
