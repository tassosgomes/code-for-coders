using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoTitleSearchTests(VideoLibraryApiFactory factory)
{
    [Fact]
    public async Task SearchMatchesTitleWithoutAccents()
    {
        var tenant = Guid.CreateVersion7();
        await SeedAsync(Create(tenant, "Injeção de dependência"), Create(tenant, "Outra aula"));
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(HttpMethod.Get, "/internal/v1/videos?q=injecao", tenant), TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Injeção de dependência", document.RootElement.GetProperty("data")[0].GetProperty("title").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task RepeatedStatusReturnsOnlyMatchingVideos()
    {
        var tenant = Guid.CreateVersion7();
        var failed = Create(tenant, "Falhou");
        failed.MarkPreparing(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(5));
        failed.MarkFailed(failed.PreparationLeaseId!.Value, VideoFailureReasons.UnreadableFile);
        var received = Create(tenant, "Recebido");
        var other = Create(tenant, "Outro");
        other.MarkPreparing(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(5));
        await SeedAsync(failed, received, other);
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(HttpMethod.Get, "/internal/v1/videos?status=failed&status=received", tenant), TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        var statuses = document.RootElement.GetProperty("data").EnumerateArray()
            .Select(video => video.GetProperty("status").GetString()).ToArray();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, statuses.Length);
        Assert.Contains("failed", statuses);
        Assert.Contains("received", statuses);
        Assert.Equal(2, document.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SearchCannotRevealAnotherTenant()
    {
        var owner = Guid.CreateVersion7();
        var requester = Guid.CreateVersion7();
        await SeedAsync(Create(owner, "Injeção secreta"));
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Request(HttpMethod.Get, "/internal/v1/videos?q=injecao", requester), TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Empty(document.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(0, document.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ColleagueCanRenameWithoutChangingStatusOrUploader()
    {
        var tenant = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        var video = Create(tenant, "Título antigo");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", tenant, actor);
        request.Headers.Add("Idempotency-Key", "rename-colleague");
        request.Content = JsonContent.Create(new { title = "Injeção de dependência" });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("received", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(video.UploadedByAccountId, document.RootElement.GetProperty("uploadedBy").GetProperty("accountId").GetGuid());
        using var search = await client.SendAsync(Request(HttpMethod.Get, "/internal/v1/videos?q=injecao", tenant), TestContext.Current.CancellationToken);
        using var page = await ReadAsync(search);
        Assert.Equal(video.VideoId, page.RootElement.GetProperty("data")[0].GetProperty("videoId").GetGuid());
    }

    [Fact]
    public async Task BlankTitleReturnsTitleRequired()
    {
        var tenant = Guid.CreateVersion7();
        var video = Create(tenant, "Original");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", tenant);
        request.Headers.Add("Idempotency-Key", "blank-title");
        request.Content = JsonContent.Create(new { title = "   " });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("TITLE_REQUIRED", document.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":null}")]
    public async Task MissingTitleReturnsTitleRequired(string body)
    {
        var tenant = Guid.CreateVersion7();
        var video = Create(tenant, "Original");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", tenant);
        request.Headers.Add("Idempotency-Key", "missing-title");
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("TITLE_REQUIRED", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TitleOverTwoHundredCharactersReturnsInvalidRequest()
    {
        var tenant = Guid.CreateVersion7();
        var video = Create(tenant, "Original");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", tenant);
        request.Headers.Add("Idempotency-Key", "long-title");
        request.Content = JsonContent.Create(new { title = new string('a', 201) });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_REQUEST", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RenameOfAnotherTenantReturnsNotFound()
    {
        var owner = Guid.CreateVersion7();
        var video = Create(owner, "Original");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", Guid.CreateVersion7());
        request.Headers.Add("Idempotency-Key", "cross-tenant-title");
        request.Content = JsonContent.Create(new { title = "Changed" });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("VIDEO_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task IdempotencyReplaysOriginalResultAndRejectsDifferentBody()
    {
        var tenant = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        var video = Create(tenant, "Original");
        await SeedAsync(video);
        using var client = factory.CreateClient();
        async Task<HttpResponseMessage> RenameAsync(string title)
        {
            using var request = Request(HttpMethod.Patch, $"/internal/v1/videos/{video.VideoId:D}", tenant, actor);
            request.Headers.Add("Idempotency-Key", "same-intention");
            request.Content = JsonContent.Create(new { title });
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        using var first = await RenameAsync("Primeiro título");
        using var replay = await RenameAsync("Primeiro título");
        using var conflict = await RenameAsync("Outro título");
        using var firstJson = await ReadAsync(first);
        using var replayJson = await ReadAsync(replay);
        using var conflictJson = await ReadAsync(conflict);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(firstJson.RootElement.ToString(), replayJson.RootElement.ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", conflictJson.RootElement.GetProperty("code").GetString());
    }

    private static Video Create(Guid tenant, string title)
        => Video.Create(tenant, title, Guid.CreateVersion7(), "Outra professora", DateTimeOffset.UtcNow);

    private async Task SeedAsync(params Video[] videos)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(videos[0].TenantId);
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        db.Videos.AddRange(videos);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, Guid tenant, Guid? actor = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateTokenForActor(tenant, actor ?? Guid.CreateVersion7(), "midia.enviar"));
        return request;
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
}
