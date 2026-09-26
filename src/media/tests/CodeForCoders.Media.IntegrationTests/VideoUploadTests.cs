using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoUploadTests(VideoLibraryApiFactory factory)
{
    private const long PartSize = 64L * 1024 * 1024;

    [Fact(DisplayName = nameof(VideoUpload_CreatesMultipartUploadWithoutExposingStorageDetails))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_CreatesMultipartUploadWithoutExposingStorageDetails()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var response = await StartUploadAsync(client, token, "upload-create-1", fileSize: (2 * PartSize) + 7);
        using var document = await ReadJsonAsync(response);
        var responseText = document.RootElement.GetRawText();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, document.RootElement.GetProperty("partCount").GetInt32());
        Assert.Equal(PartSize, document.RootElement.GetProperty("partSize").GetInt64());
        Assert.Empty(document.RootElement.GetProperty("receivedParts").EnumerateArray());
        Assert.False(responseText.Contains("storageUploadId", StringComparison.OrdinalIgnoreCase));
        Assert.False(responseText.Contains(factory.MinioEndpoint, StringComparison.Ordinal));
        Assert.False(responseText.Contains(MediaIntegrationFixture.MinioBucketName, StringComparison.Ordinal));
    }

    [Fact(DisplayName = nameof(VideoUpload_RejectsBlankTitleBeforeStartingStorage))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_RejectsBlankTitleBeforeStartingStorage()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var response = await StartUploadAsync(client, token, "upload-empty-title", title: "   ");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("TITLE_REQUIRED", await ReadCodeAsync(response));
        Assert.Empty(await ListMultipartUploadsAsync(tenantId));
    }

    [Fact(DisplayName = nameof(VideoUpload_RejectsSixGiBFileBeforeStartingStorage))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_RejectsSixGiBFileBeforeStartingStorage()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var response = await StartUploadAsync(client, token, "upload-too-large", fileSize: 6L * 1024 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("FILE_TOO_LARGE", await ReadCodeAsync(response));
        Assert.Empty(await ListMultipartUploadsAsync(tenantId));
    }

    [Fact(DisplayName = nameof(VideoUpload_RejectsUnsupportedExtensionAndMimeBeforeStartingStorage))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_RejectsUnsupportedExtensionAndMimeBeforeStartingStorage()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var response = await StartUploadAsync(
            client,
            token,
            "upload-avi",
            fileName: "aula.avi",
            contentType: "video/x-msvideo");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("FORMAT_NOT_SUPPORTED", await ReadCodeAsync(response));
        Assert.Empty(await ListMultipartUploadsAsync(tenantId));
    }

    [Fact(DisplayName = nameof(VideoUpload_UsesPublicSignedUrlOnlyForRequestedParts))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_UsesPublicSignedUrlOnlyForRequestedParts()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-signed-urls", fileSize: (2 * PartSize) + 1);
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        using var response = await RequestPartUrlsAsync(client, token, uploadId, [1, 3]);
        using var document = await ReadJsonAsync(response);
        var parts = document.RootElement.GetProperty("parts").EnumerateArray().ToArray();
        var responseText = document.RootElement.GetRawText();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 1, 3 }, parts.Select(part => part.GetProperty("partNumber").GetInt32()));
        Assert.All(parts, part => Assert.Equal(factory.MinioEndpoint, new Uri(part.GetProperty("url").GetString()!).GetLeftPart(UriPartial.Authority)));
        Assert.All(parts, part => Assert.Equal("http", new Uri(part.GetProperty("url").GetString()!).Scheme));
        Assert.Equal(2, CountOccurrences(responseText, factory.MinioEndpoint));
        Assert.All(parts, part => Assert.InRange(
            part.GetProperty("expiresAt").GetDateTimeOffset(),
            DateTimeOffset.UtcNow.AddMinutes(59),
            DateTimeOffset.UtcNow.AddMinutes(61)));
    }

    [Fact(DisplayName = nameof(VideoUpload_RejectsOutOfRangePartBeforeSigning))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_RejectsOutOfRangePartBeforeSigning()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-out-of-range");
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        using var response = await RequestPartUrlsAsync(client, token, uploadId, [2]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PART_OUT_OF_RANGE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoUpload_ReportsIncompleteUploadWhenAPartIsMissing))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_ReportsIncompleteUploadWhenAPartIsMissing()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-incomplete", fileSize: PartSize + 1);
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        var urls = await GetPartUrlsAsync(client, token, uploadId, [1, 2]);
        using var partResponse = await PutPartAsync(urls.Single(part => part.PartNumber == 1).Url, PartSize);
        Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);

        using var response = await CompleteUploadAsync(client, token, uploadId, "upload-incomplete-complete");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("UPLOAD_INCOMPLETE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoUpload_CompletesThreePartsAndKeepsOriginalPrivate))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_CompletesThreePartsAndKeepsOriginalPrivate()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-three-parts", fileSize: (2 * PartSize) + 11);
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        var urls = await GetPartUrlsAsync(client, token, uploadId, [1, 2, 3]);

        foreach (var part in urls)
        {
            using var partResponse = await PutPartAsync(part.Url, part.PartNumber == 3 ? 11 : PartSize);
            Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);
        }

        using var response = await CompleteUploadAsync(client, token, uploadId, "upload-three-parts-complete");
        using var document = await ReadJsonAsync(response);
        var videoId = document.RootElement.GetProperty("videoId").GetGuid();
        var objectKey = $"media/{tenantId:D}/{videoId:D}/original";

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("received", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Marina Alves", document.RootElement.GetProperty("uploadedBy").GetProperty("name").GetString());
        using var s3 = CreateMinioClient();
        var head = await s3.GetObjectMetadataAsync(
            new GetObjectMetadataRequest { BucketName = MediaIntegrationFixture.MinioBucketName, Key = objectKey },
            TestContext.Current.CancellationToken);
        Assert.Equal((2 * PartSize) + 11, head.ContentLength);

        using var anonymous = new HttpClient();
        using var publicResponse = await anonymous.GetAsync(
            $"{factory.MinioEndpoint}/{MediaIntegrationFixture.MinioBucketName}/{objectKey}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, publicResponse.StatusCode);
    }

    [Fact(DisplayName = nameof(VideoUpload_ReplayingCompletionReturnsTheSameVideo))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_ReplayingCompletionReturnsTheSameVideo()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-idempotent-complete");
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        var urls = await GetPartUrlsAsync(client, token, uploadId, [1]);
        using var partResponse = await PutPartAsync(urls[0].Url, 17);
        Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);

        using var first = await CompleteUploadAsync(client, token, uploadId, "upload-complete-idempotency");
        using var firstDocument = await ReadJsonAsync(first);
        using var replay = await CompleteUploadAsync(client, token, uploadId, "upload-complete-idempotency");
        using var replayDocument = await ReadJsonAsync(replay);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(firstDocument.RootElement.GetProperty("videoId").GetGuid(), replayDocument.RootElement.GetProperty("videoId").GetGuid());
    }

    [Fact(DisplayName = nameof(VideoUpload_ConcurrentCompletionCreatesOnlyOneVideo))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_ConcurrentCompletionCreatesOnlyOneVideo()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-concurrent-complete");
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        var urls = await GetPartUrlsAsync(client, token, uploadId, [1]);
        using var partResponse = await PutPartAsync(urls[0].Url, 17);
        Assert.Equal(HttpStatusCode.OK, partResponse.StatusCode);

        var completions = await Task.WhenAll(
            CompleteUploadAsync(client, token, uploadId, "upload-concurrent-complete-1"),
            CompleteUploadAsync(client, token, uploadId, "upload-concurrent-complete-2"));
        using var first = completions[0];
        using var second = completions[1];
        using var firstDocument = await ReadJsonAsync(first);
        using var secondDocument = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstDocument.RootElement.GetProperty("videoId").GetGuid(), secondDocument.RootElement.GetProperty("videoId").GetGuid());
    }

    [Fact(DisplayName = nameof(VideoUpload_HidesPendingUploadFromAnotherActor))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_HidesPendingUploadFromAnotherActor()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var ownerToken = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        var otherActorToken = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, ownerToken, "upload-owner-only");
        using var createdDocument = await ReadJsonAsync(created);
        var uploadId = createdDocument.RootElement.GetProperty("uploadId").GetGuid();
        using var request = AuthorizedRequest(HttpMethod.Get, $"/internal/v1/video-uploads/{uploadId:D}", otherActorToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("UPLOAD_NOT_FOUND", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoUpload_RejectsReusedIdempotencyKeyWithDifferentRequest))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_RejectsReusedIdempotencyKeyWithDifferentRequest()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var first = await StartUploadAsync(client, token, "upload-key-reused", title: "Aula original");
        using var conflict = await StartUploadAsync(client, token, "upload-key-reused", title: "Outra aula");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ReadCodeAsync(conflict));
    }

    [Fact(DisplayName = nameof(VideoUpload_MapsUnavailableStorageTo503WithoutPersistingUpload))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_MapsUnavailableStorageTo503WithoutPersistingUpload()
    {
        using var unavailableFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMediaStoragePort>();
                services.AddSingleton<IMediaStoragePort, UnavailableMediaStoragePort>();
            });
        });
        using var client = unavailableFactory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var response = await StartUploadAsync(client, token, "upload-storage-unavailable");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("STORAGE_UNAVAILABLE", await ReadCodeAsync(response));
        Assert.Empty(await ListMultipartUploadsAsync(tenantId));
    }

    [Fact(DisplayName = nameof(VideoUpload_MapsUnavailableStorageTo503WhenGettingUpload))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_MapsUnavailableStorageTo503WhenGettingUpload()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-storage-unavailable-get");
        using var createdBody = await ReadJsonAsync(created);
        var uploadId = createdBody.RootElement.GetProperty("uploadId").GetGuid();
        using var unavailableFactory = CreateUnavailableStorageFactory();
        using var unavailableClient = unavailableFactory.CreateClient();
        using var request = AuthorizedRequest(HttpMethod.Get, $"/internal/v1/video-uploads/{uploadId:D}", token);

        using var response = await unavailableClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("STORAGE_UNAVAILABLE", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoUpload_MapsUnavailableStorageTo503WhenCompletingUpload))]
    [Trait("Layer", "Media video upload - Integration")]
    public async Task VideoUpload_MapsUnavailableStorageTo503WhenCompletingUpload()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "upload-storage-unavailable-complete");
        using var createdBody = await ReadJsonAsync(created);
        var uploadId = createdBody.RootElement.GetProperty("uploadId").GetGuid();
        using var unavailableFactory = CreateUnavailableStorageFactory();
        using var unavailableClient = unavailableFactory.CreateClient();
        using var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/complete", token);
        request.Headers.Add("Idempotency-Key", "upload-storage-unavailable-complete-key");

        using var response = await unavailableClient.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("STORAGE_UNAVAILABLE", await ReadCodeAsync(response));
    }

    private WebApplicationFactory<Program> CreateUnavailableStorageFactory()
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMediaStoragePort>();
            services.AddSingleton<IMediaStoragePort, UnavailableMediaStoragePort>();
        }));

    private static async Task<HttpResponseMessage> StartUploadAsync(
        HttpClient client,
        string token,
        string idempotencyKey,
        string title = "Aula de integração",
        string fileName = "aula.mp4",
        long fileSize = 17,
        string contentType = "video/mp4")
    {
        var request = AuthorizedRequest(HttpMethod.Post, "/internal/v1/video-uploads", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            title,
            fileName,
            fileSize,
            contentType,
            fingerprint = "video-upload-fingerprint-123",
            uploaderName = "Marina Alves",
        });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> RequestPartUrlsAsync(
        HttpClient client,
        string token,
        Guid uploadId,
        int[] partNumbers)
    {
        var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/part-urls", token);
        request.Content = JsonContent.Create(new { partNumbers });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<PartUrl>> GetPartUrlsAsync(
        HttpClient client,
        string token,
        Guid uploadId,
        int[] partNumbers)
    {
        using var response = await RequestPartUrlsAsync(client, token, uploadId, partNumbers);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        return document.RootElement.GetProperty("parts")
            .EnumerateArray()
            .Select(part => new PartUrl(
                part.GetProperty("partNumber").GetInt32(),
                new Uri(part.GetProperty("url").GetString()!, UriKind.Absolute)))
            .ToArray();
    }

    private static async Task<HttpResponseMessage> PutPartAsync(Uri url, long length)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(new byte[checked((int)length)]),
        };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> CompleteUploadAsync(
        HttpClient client,
        string token,
        Guid uploadId,
        string idempotencyKey)
    {
        var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/complete", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<MultipartUpload>> ListMultipartUploadsAsync(Guid tenantId)
    {
        using var client = CreateMinioClient();
        var result = await client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
        {
            BucketName = MediaIntegrationFixture.MinioBucketName,
            Prefix = $"media/{tenantId:D}/",
        }, TestContext.Current.CancellationToken);
        return result.MultipartUploads ?? [];
    }

    private AmazonS3Client CreateMinioClient()
        => new(
            new BasicAWSCredentials(MediaIntegrationFixture.MinioAccessKey, MediaIntegrationFixture.MinioSecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                UseHttp = true,
                ServiceURL = factory.MinioEndpoint,
            });

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static int CountOccurrences(string value, string match)
    {
        var count = 0;
        var position = 0;
        while ((position = value.IndexOf(match, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += match.Length;
        }

        return count;
    }

    private sealed record PartUrl(int PartNumber, Uri Url);

    private sealed class UnavailableMediaStoragePort : IMediaStoragePort
    {
        public Task<string> InitiateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
            => Task.FromException<string>(new StorageUnavailableException());

        public Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<MediaUploadPart>>(new StorageUnavailableException());

        public Task<Uri> CreatePartUploadUriAsync(
            string objectKey,
            string storageUploadId,
            int partNumber,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
            => Task.FromException<Uri>(new StorageUnavailableException());

        public Task CompleteMultipartUploadAsync(
            string objectKey,
            string storageUploadId,
            IReadOnlyList<MediaUploadPart> parts,
            CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());

        public Task AbortMultipartUploadAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());

        public Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());

        public Task UploadDirectoryAsync(string sourceDirectory, string objectPrefix, CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());

        public Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken)
            => Task.FromException(new StorageUnavailableException());
    }
}
