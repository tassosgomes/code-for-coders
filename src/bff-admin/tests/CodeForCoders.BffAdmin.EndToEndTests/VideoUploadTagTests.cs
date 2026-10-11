using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class VideoUploadTagTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(ForwardsRawTagsAndPreservesCanonicalCreateResponse))]
    [Trait("Layer", "Video upload tags - EndToEnd")]
    public async Task ForwardsRawTagsAndPreservesCanonicalCreateResponse()
    {
        ResetState(); using var client = factory.CreateClient(); var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-tag-token");
        factory.VideoUploadHandler.Tags = ["Docker", "Módulo 2"];
        string[] raw = [" docker ", "DOCKER", "Módulo 2"];
        using var request = UploadRequest(login, raw);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "Docker", "Módulo 2" }, await TagsAsync(response));
        using var body = JsonDocument.Parse(factory.VideoUploadHandler.LastBody!);
        Assert.Equal(raw, body.RootElement.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal("Marina Alves", body.RootElement.GetProperty("uploaderName").GetString());
        Assert.Equal("teacher-tag-token", factory.VideoUploadHandler.LastAccessToken);
        Assert.Equal("upload-tag-key", factory.VideoUploadHandler.LastIdempotencyKey);
        Assert.Equal(1, factory.VideoUploadHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(ResumptionReturnsPendingTagsAndOkStatus))]
    [Trait("Layer", "Video upload tags - EndToEnd")]
    public async Task ResumptionReturnsPendingTagsAndOkStatus()
    {
        ResetState(); using var client = factory.CreateClient(); var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-tag-token");
        factory.VideoUploadHandler.CreateStatusCode = HttpStatusCode.OK;
        factory.VideoUploadHandler.Tags = ["Original"];
        using var request = UploadRequest(login, ["Other"]);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "Original" }, await TagsAsync(response));
    }

    [Theory(DisplayName = nameof(ForwardsTagBusinessErrorsWithoutLocalValidation))]
    [InlineData("TAG_INVALID")]
    [InlineData("TAGS_LIMIT_EXCEEDED")]
    [Trait("Layer", "Video upload tags - EndToEnd")]
    public async Task ForwardsTagBusinessErrorsWithoutLocalValidation(string code)
    {
        ResetState(); using var client = factory.CreateClient(); var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-tag-token");
        factory.VideoUploadHandler.StatusCode = HttpStatusCode.UnprocessableEntity;
        factory.VideoUploadHandler.ProblemCode = code;
        using var request = UploadRequest(login, [" "]);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await ReadCodeAsync(response));
        Assert.Equal(1, factory.VideoUploadHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(CompletionPreservesVideoTags))]
    [Trait("Layer", "Video upload tags - EndToEnd")]
    public async Task CompletionPreservesVideoTags()
    {
        ResetState(); using var client = factory.CreateClient(); var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = TeacherSession(login.Session.AccountId, "teacher-tag-token");
        factory.VideoUploadHandler.Tags = ["Docker"];
        using var request = CreateRequest(HttpMethod.Post, $"/api/v1/video-uploads/{Guid.CreateVersion7()}/complete", login);
        request.Headers.Add("Idempotency-Key", "complete-tag-key");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "Docker" }, await TagsAsync(response));
    }

    private static HttpRequestMessage UploadRequest(LoginResult login, string[] tags)
    {
        var request = CreateRequest(HttpMethod.Post, "/api/v1/video-uploads", login);
        request.Headers.Add("Idempotency-Key", "upload-tag-key");
        request.Content = JsonContent.Create(new { title = "Aula", fileName = "aula.mp4", fileSize = 3, contentType = "video/mp4", fingerprint = "video-upload-tag-fingerprint", tags });
        return request;
    }

    private static async Task<string[]> TagsAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        return json.RootElement.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray();
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
