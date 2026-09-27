using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoUploadTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoUpload_AppendsValidatedUploaderAndForwardsIdempotencyKey))]
    [Trait("Layer", "BffAdmin video upload - EndToEnd")]
    public async Task VideoUpload_AppendsValidatedUploaderAndForwardsIdempotencyKey()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-upload-token");

        using var response = await CreateUploadAsync(client, login, "upload-key-1");
        using var body = JsonDocument.Parse(factory.VideoUploadHandler.LastBody!);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/api/v1/video-uploads/", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal("teacher-upload-token", factory.VideoUploadHandler.LastAccessToken);
        Assert.Equal("upload-key-1", factory.VideoUploadHandler.LastIdempotencyKey);
        Assert.Equal("Marina Alves", body.RootElement.GetProperty("uploaderName").GetString());
        Assert.Equal("/internal/v1/video-uploads", factory.VideoUploadHandler.LastPath);
    }

    [Fact(DisplayName = nameof(VideoUpload_RefusesActorWithoutPermissionBeforeCallingMedia))]
    [Trait("Layer", "BffAdmin video upload - EndToEnd")]
    public async Task VideoUpload_RefusesActorWithoutPermissionBeforeCallingMedia()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            login.Session.AccountId,
            "Marina Alves",
            ["suporte"],
            ["suporte.atender"],
            DateTimeOffset.UtcNow.AddMinutes(30),
            "support-token");

        using var response = await CreateUploadAsync(client, login, "upload-key-2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", await ReadCodeAsync(response));
        Assert.Equal(0, factory.VideoUploadHandler.RequestCount);
        Assert.Null(factory.StaffSessionIdentityHandler.LastValidationAudience);
    }

    [Fact(DisplayName = nameof(VideoUpload_MapsStorageUnavailableToBadGateway))]
    [Trait("Layer", "BffAdmin video upload - EndToEnd")]
    public async Task VideoUpload_MapsStorageUnavailableToBadGateway()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-upload-token");
        factory.VideoUploadHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        factory.VideoUploadHandler.ProblemCode = "STORAGE_UNAVAILABLE";

        using var response = await CreateUploadAsync(client, login, "upload-key-3");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MEDIA_UNAVAILABLE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoUpload_ForwardsPartNumbersToMedia))]
    [Trait("Layer", "BffAdmin video upload - EndToEnd")]
    public async Task VideoUpload_ForwardsPartNumbersToMedia()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-upload-token");
        var uploadId = Guid.CreateVersion7();
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/video-uploads/{uploadId:D}/part-urls",
            login,
            JsonContent.Create(new CreateVideoUploadPartUrlsRequest([1, 2])));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/internal/v1/video-uploads/{uploadId:D}/part-urls", factory.VideoUploadHandler.LastPath);
        Assert.Equal("teacher-upload-token", factory.VideoUploadHandler.LastAccessToken);
        using var body = JsonDocument.Parse(factory.VideoUploadHandler.LastBody!);
        Assert.Equal(new[] { 1, 2 }, body.RootElement.GetProperty("partNumbers").EnumerateArray().Select(part => part.GetInt32()));
    }

    [Fact(DisplayName = nameof(VideoUpload_ForwardsCompletionIdempotencyKeyAndReturnsLocation))]
    [Trait("Layer", "BffAdmin video upload - EndToEnd")]
    public async Task VideoUpload_ForwardsCompletionIdempotencyKeyAndReturnsLocation()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-upload-token");
        var uploadId = Guid.CreateVersion7();
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/video-uploads/{uploadId:D}/complete", login);
        request.Headers.Add("Idempotency-Key", "completion-key");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/api/v1/videos/", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal($"/internal/v1/video-uploads/{uploadId:D}/complete", factory.VideoUploadHandler.LastPath);
        Assert.Equal("completion-key", factory.VideoUploadHandler.LastIdempotencyKey);
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
        request.Headers.Add("Idempotency-Key", "video-upload-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static Task<HttpResponseMessage> CreateUploadAsync(HttpClient client, LoginResult login, string idempotencyKey)
    {
        var request = CreateRequest(
            HttpMethod.Post,
            "/api/v1/video-uploads",
            login,
            JsonContent.Create(new CreateVideoUploadRequest(
                "Aula de teste",
                "aula.mp4",
                3,
                "video/mp4",
                "video-upload-fingerprint-123")));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        LoginResult login,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
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
