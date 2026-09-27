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
using CodeForCoders.Media.Application.UseCases.VideoUploads.ExpirePendingVideoUploads;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoUploadResumeTests
{
    private const string Fingerprint = "video-upload-resume-fingerprint-123";
    private readonly VideoLibraryApiFactory factory;

    public VideoUploadResumeTests(VideoLibraryApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ReturnsStoragePartsForTheSameActorAndTenant))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ReturnsStoragePartsForTheSameActorAndTenant()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var token = factory.CreateTokenForActor(tenantId, actorId, "midia.enviar");
        using var started = await StartUploadAsync(client, token, "resume-first");
        using var startedBody = await ReadJsonAsync(started);
        var uploadId = startedBody.RootElement.GetProperty("uploadId").GetGuid();
        var partUrl = await GetPartUrlAsync(client, token, uploadId);
        using var uploadedPart = await PutPartAsync(partUrl, 17);
        Assert.Equal(HttpStatusCode.OK, uploadedPart.StatusCode);

        using var resumed = await StartUploadAsync(client, token, "resume-second");
        using var resumedBody = await ReadJsonAsync(resumed);

        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(uploadId, resumedBody.RootElement.GetProperty("uploadId").GetGuid());
        Assert.Equal(new[] { 1 }, resumedBody.RootElement.GetProperty("receivedParts").EnumerateArray().Select(part => part.GetInt32()));
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ScopesFingerprintAndIdempotencyByTenantAndActor))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ScopesFingerprintAndIdempotencyByTenantAndActor()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var otherActorId = Guid.CreateVersion7();
        var firstToken = factory.CreateTokenForActor(tenantId, actorId, "midia.enviar");
        var otherTenantToken = factory.CreateTokenForActor(otherTenantId, actorId, "midia.enviar");
        var otherActorToken = factory.CreateTokenForActor(tenantId, otherActorId, "midia.enviar");

        using var first = await StartUploadAsync(client, firstToken, "tenant-scoped-key");
        using var otherTenant = await StartUploadAsync(client, otherTenantToken, "tenant-scoped-key");
        using var otherActor = await StartUploadAsync(client, otherActorToken, "tenant-scoped-key");
        using var firstBody = await ReadJsonAsync(first);
        using var otherTenantBody = await ReadJsonAsync(otherTenant);
        using var otherActorBody = await ReadJsonAsync(otherActor);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherTenant.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherActor.StatusCode);
        Assert.NotEqual(firstBody.RootElement.GetProperty("uploadId").GetGuid(), otherTenantBody.RootElement.GetProperty("uploadId").GetGuid());
        Assert.NotEqual(firstBody.RootElement.GetProperty("uploadId").GetGuid(), otherActorBody.RootElement.GetProperty("uploadId").GetGuid());
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ListsOnlyTheCurrentActorAndTenantWithParts))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ListsOnlyTheCurrentActorAndTenantWithParts()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var otherActorId = Guid.CreateVersion7();
        var ownerToken = factory.CreateTokenForActor(tenantId, actorId, "midia.enviar");
        var otherActorToken = factory.CreateTokenForActor(tenantId, otherActorId, "midia.enviar");
        var otherTenantToken = factory.CreateTokenForActor(otherTenantId, actorId, "midia.enviar");

        using var ownerUpload = await StartUploadAsync(client, ownerToken, "pending-owner");
        using var ownerBody = await ReadJsonAsync(ownerUpload);
        var ownerUploadId = ownerBody.RootElement.GetProperty("uploadId").GetGuid();
        var partUrl = await GetPartUrlAsync(client, ownerToken, ownerUploadId);
        using var uploadedPart = await PutPartAsync(partUrl, 17);
        Assert.Equal(HttpStatusCode.OK, uploadedPart.StatusCode);
        using var otherActorUpload = await StartUploadAsync(client, otherActorToken, "pending-other-actor");
        using var otherTenantUpload = await StartUploadAsync(client, otherTenantToken, "pending-other-tenant");

        using var ownerList = await ListPendingAsync(client, ownerToken);
        using var actorList = await ListPendingAsync(client, otherActorToken);
        using var tenantList = await ListPendingAsync(client, otherTenantToken);
        using var ownerListBody = await ReadJsonAsync(ownerList);
        using var actorListBody = await ReadJsonAsync(actorList);
        using var tenantListBody = await ReadJsonAsync(tenantList);

        Assert.Equal(HttpStatusCode.OK, ownerList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, actorList.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tenantList.StatusCode);
        Assert.Equal(new[] { ownerUploadId }, ReadUploadIds(ownerListBody));
        Assert.Equal(new[] { 1 }, ownerListBody.RootElement.GetProperty("data")[0].GetProperty("receivedParts").EnumerateArray().Select(part => part.GetInt32()));
        Assert.Equal(new[] { (await ReadUploadIdAsync(otherActorUpload)) }, ReadUploadIds(actorListBody));
        Assert.Equal(new[] { (await ReadUploadIdAsync(otherTenantUpload)) }, ReadUploadIds(tenantListBody));
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ExpirationAbortsPartsHidesUploadAndAllowsANewUpload))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ExpirationAbortsPartsHidesUploadAndAllowsANewUpload()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var clockFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = clockFactory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var token = factory.CreateToken(tenantId, permissions: ["midia.enviar"]);
        using var created = await StartUploadAsync(client, token, "expire-old");
        using var createdBody = await ReadJsonAsync(created);
        var uploadId = createdBody.RootElement.GetProperty("uploadId").GetGuid();
        var storageUpload = await GetStorageUploadAsync(clockFactory.Services, uploadId);
        var partUrl = await GetPartUrlAsync(client, token, uploadId);
        using var uploadedPart = await PutPartAsync(partUrl, 17);
        Assert.Equal(HttpStatusCode.OK, uploadedPart.StatusCode);
        clock.Advance(TimeSpan.FromHours(25));

        using (var expiredRead = await GetUploadAsync(client, token, uploadId))
        {
            Assert.Equal(HttpStatusCode.NotFound, expiredRead.StatusCode);
            Assert.Equal("UPLOAD_NOT_FOUND", await ReadCodeAsync(expiredRead));
        }

        using (var scope = clockFactory.Services.CreateAsyncScope())
        {
            var expireUploads = scope.ServiceProvider.GetRequiredService<IExpirePendingVideoUploads>();
            Assert.True(await expireUploads.ExpireAsync(uploadId, TestContext.Current.CancellationToken));
        }

        using (var storage = CreateMinioClient())
        {
            var missingParts = await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ListPartsAsync(new ListPartsRequest
            {
                BucketName = MediaIntegrationFixture.MinioBucketName,
                Key = $"media/{storageUpload.ObjectKey}",
                UploadId = storageUpload.StorageUploadId,
            }, TestContext.Current.CancellationToken));
            Assert.Equal("NoSuchUpload", missingParts.ErrorCode);
        }
        using var pending = await ListPendingAsync(client, token);
        using var pendingBody = await ReadJsonAsync(pending);
        Assert.Empty(pendingBody.RootElement.GetProperty("data").EnumerateArray());

        using var restarted = await StartUploadAsync(client, token, "expire-new");
        using var restartedBody = await ReadJsonAsync(restarted);
        Assert.Equal(HttpStatusCode.Created, restarted.StatusCode);
        Assert.NotEqual(uploadId, restartedBody.RootElement.GetProperty("uploadId").GetGuid());
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ExpiredFingerprintStartsANewUploadWithoutTheSweep))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ExpiredFingerprintStartsANewUploadWithoutTheSweep()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var clockFactory = WithClock(clock);
        using var client = clockFactory.CreateClient();
        var token = factory.CreateTokenForActor(Guid.CreateVersion7(), Guid.CreateVersion7(), "midia.enviar");
        using var created = await StartUploadAsync(client, token, "expired-fingerprint-first");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var uploadId = await ReadUploadIdAsync(created);
        var partUrl = await GetPartUrlAsync(client, token, uploadId);
        using var uploadedPart = await PutPartAsync(partUrl, 17);
        Assert.Equal(HttpStatusCode.OK, uploadedPart.StatusCode);
        var storedUpload = await GetStorageUploadAsync(clockFactory.Services, uploadId);
        clock.SetUtcNow(storedUpload.ExpiresAt.AddMinutes(1));

        using var restarted = await StartUploadAsync(client, token, "expired-fingerprint-second");
        Assert.Equal(HttpStatusCode.Created, restarted.StatusCode);
        Assert.NotEqual(uploadId, await ReadUploadIdAsync(restarted));
        Assert.NotNull((await GetStorageUploadAsync(clockFactory.Services, uploadId)).ExpiredAt);
        await AssertMultipartUploadAbortedAsync(storedUpload);
    }

    [Fact(DisplayName = nameof(VideoUploadResume_SweepExpiresOnlyAfterTheDeadline))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_SweepExpiresOnlyAfterTheDeadline()
    {
        var clock = new AdjustableTimeProvider(DateTimeOffset.UtcNow);
        using var clockFactory = WithClock(clock);
        using var client = clockFactory.CreateClient();
        var token = factory.CreateTokenForActor(Guid.CreateVersion7(), Guid.CreateVersion7(), "midia.enviar");
        using var created = await StartUploadAsync(client, token, "sweep-deadline");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var uploadId = await ReadUploadIdAsync(created);
        var partUrl = await GetPartUrlAsync(client, token, uploadId);
        using var uploadedPart = await PutPartAsync(partUrl, 17);
        Assert.Equal(HttpStatusCode.OK, uploadedPart.StatusCode);
        var storedUpload = await GetStorageUploadAsync(clockFactory.Services, uploadId);

        clock.SetUtcNow(storedUpload.ExpiresAt.AddMinutes(-1));
        await RunExpirationSweepAsync(clockFactory.Services);
        Assert.Null((await GetStorageUploadAsync(clockFactory.Services, uploadId)).ExpiredAt);
        using (var storage = CreateMinioClient())
        {
            var parts = await storage.ListPartsAsync(CreateListPartsRequest(storedUpload), TestContext.Current.CancellationToken);
            Assert.Single(parts.Parts);
        }

        clock.SetUtcNow(storedUpload.ExpiresAt);
        await RunExpirationSweepAsync(clockFactory.Services);
        Assert.NotNull((await GetStorageUploadAsync(clockFactory.Services, uploadId)).ExpiredAt);
        await AssertMultipartUploadAbortedAsync(storedUpload);
    }

    [Fact(DisplayName = nameof(VideoUploadResume_DoesNotCountExternallyRemovedMultipartUpload))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_DoesNotCountExternallyRemovedMultipartUpload()
    {
        using var client = factory.CreateClient();
        var token = factory.CreateTokenForActor(Guid.CreateVersion7(), Guid.CreateVersion7(), "midia.enviar");
        using var created = await StartUploadAsync(client, token, "removed-storage-upload");
        var uploadId = await ReadUploadIdAsync(created);
        var storageUpload = await GetStorageUploadAsync(factory.Services, uploadId);
        using (var storage = CreateMinioClient())
        {
            await storage.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = MediaIntegrationFixture.MinioBucketName,
                Key = $"media/{storageUpload.ObjectKey}",
                UploadId = storageUpload.StorageUploadId,
            }, TestContext.Current.CancellationToken);
        }

        using var response = await ListPendingAsync(client, token);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(0, body.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ConcurrentFingerprintsCreateOnlyOnePendingUpload))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ConcurrentFingerprintsCreateOnlyOnePendingUpload()
    {
        var storage = new BarrierMediaStoragePort();
        using var concurrentFactory = WithStorage(storage);
        using var client = concurrentFactory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        var responses = await Task.WhenAll(
            StartUploadAsync(client, token, "concurrent-fingerprint-1"),
            StartUploadAsync(client, token, "concurrent-fingerprint-2"));
        using var first = responses[0];
        using var second = responses[1];
        using var firstBody = await ReadJsonAsync(first);
        using var secondBody = await ReadJsonAsync(second);

        Assert.Contains(first.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        Assert.Contains(second.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        Assert.Equal(firstBody.RootElement.GetProperty("uploadId").GetGuid(), secondBody.RootElement.GetProperty("uploadId").GetGuid());
        Assert.Equal(2, storage.InitiateCount);
        Assert.Equal(1, storage.AbortCount);
    }

    [Fact(DisplayName = nameof(VideoUploadResume_ConcurrentIdempotencyKeyReplaysTheCommittedResponse))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_ConcurrentIdempotencyKeyReplaysTheCommittedResponse()
    {
        var storage = new BarrierMediaStoragePort();
        using var concurrentFactory = WithStorage(storage);
        using var client = concurrentFactory.CreateClient();
        var token = factory.CreateToken(Guid.CreateVersion7(), permissions: ["midia.enviar"]);
        var responses = await Task.WhenAll(
            StartUploadAsync(client, token, "same-concurrent-key"),
            StartUploadAsync(client, token, "same-concurrent-key"));
        using var first = responses[0];
        using var second = responses[1];
        using var firstBody = await ReadJsonAsync(first);
        using var secondBody = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstBody.RootElement.GetProperty("uploadId").GetGuid(), secondBody.RootElement.GetProperty("uploadId").GetGuid());
        Assert.Equal(2, storage.InitiateCount);
        Assert.Equal(1, storage.AbortCount);
    }

    [Fact(DisplayName = nameof(VideoUploadResume_MapsUnavailableStorageTo503WhenResuming))]
    [Trait("Layer", "Media video upload resume - Integration")]
    public async Task VideoUploadResume_MapsUnavailableStorageTo503WhenResuming()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var token = factory.CreateTokenForActor(tenantId, actorId, "midia.enviar");
        using var created = await StartUploadAsync(client, token, "resume-storage-first");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var unavailableFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMediaStoragePort>();
            services.AddSingleton<IMediaStoragePort, UnavailableMediaStoragePort>();
        }));
        using var unavailableClient = unavailableFactory.CreateClient();
        using var resumed = await StartUploadAsync(unavailableClient, token, "resume-storage-second");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resumed.StatusCode);
        Assert.Equal("STORAGE_UNAVAILABLE", await ReadCodeAsync(resumed));
    }

    private WebApplicationFactory<Program> WithStorage(IMediaStoragePort storage)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMediaStoragePort>();
            services.AddSingleton(storage);
        }));

    private WebApplicationFactory<Program> WithClock(TimeProvider clock)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        }));

    private static async Task RunExpirationSweepAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var expireUploads = scope.ServiceProvider.GetRequiredService<IExpirePendingVideoUploads>();
        await expireUploads.ExecuteAsync(500, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> StartUploadAsync(HttpClient client, string token, string idempotencyKey)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, "/internal/v1/video-uploads", token);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            title = "Aula retomável",
            fileName = "aula-retomavel.mp4",
            fileSize = 17,
            contentType = "video/mp4",
            fingerprint = Fingerprint,
            uploaderName = "Marina Alves",
        });
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<Uri> GetPartUrlAsync(HttpClient client, string token, Guid uploadId)
    {
        using var request = AuthorizedRequest(HttpMethod.Post, $"/internal/v1/video-uploads/{uploadId:D}/part-urls", token);
        request.Content = JsonContent.Create(new { partNumbers = new[] { 1 } });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        return new Uri(body.RootElement.GetProperty("parts")[0].GetProperty("url").GetString()!, UriKind.Absolute);
    }

    private static async Task<HttpResponseMessage> PutPartAsync(Uri url, int size)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new ByteArrayContent(new byte[size]) };
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> GetUploadAsync(HttpClient client, string token, Guid uploadId)
        => client.SendAsync(
            AuthorizedRequest(HttpMethod.Get, $"/internal/v1/video-uploads/{uploadId:D}", token),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> ListPendingAsync(HttpClient client, string token)
        => client.SendAsync(
            AuthorizedRequest(HttpMethod.Get, "/internal/v1/video-uploads?_page=1&_size=10", token),
            TestContext.Current.CancellationToken);

    private static async Task<StoredUpload> GetStorageUploadAsync(IServiceProvider serviceProvider, Guid uploadId)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var upload = await dbContext.VideoUploads.IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.UploadId == uploadId, TestContext.Current.CancellationToken);
        return new StoredUpload(upload.ObjectKey, upload.StorageUploadId, upload.ExpiresAt, upload.ExpiredAt);
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

    private async Task AssertMultipartUploadAbortedAsync(StoredUpload upload)
    {
        using var storage = CreateMinioClient();
        var missingParts = await Assert.ThrowsAsync<AmazonS3Exception>(() => storage.ListPartsAsync(
            CreateListPartsRequest(upload),
            TestContext.Current.CancellationToken));
        Assert.Equal("NoSuchUpload", missingParts.ErrorCode);
    }

    private static ListPartsRequest CreateListPartsRequest(StoredUpload upload)
        => new()
        {
            BucketName = MediaIntegrationFixture.MinioBucketName,
            Key = $"media/{upload.ObjectKey}",
            UploadId = upload.StorageUploadId,
        };

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
        using var body = await ReadJsonAsync(response);
        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<Guid> ReadUploadIdAsync(HttpResponseMessage response)
    {
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("uploadId").GetGuid();
    }

    private static Guid[] ReadUploadIds(JsonDocument body)
        => body.RootElement.GetProperty("data").EnumerateArray()
            .Select(upload => upload.GetProperty("uploadId").GetGuid())
            .ToArray();

    private sealed record StoredUpload(
        string ObjectKey,
        string StorageUploadId,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? ExpiredAt);

    private sealed class BarrierMediaStoragePort : IMediaStoragePort
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int initiateCount;
        private int abortCount;

        public int InitiateCount => Volatile.Read(ref initiateCount);

        public int AbortCount => Volatile.Read(ref abortCount);

        public async Task<string> InitiateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref initiateCount) == 2)
            {
                release.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
            return Guid.CreateVersion7().ToString("N");
        }

        public Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MediaUploadPart>>([]);

        public Task<Uri> CreatePartUploadUriAsync(string objectKey, string storageUploadId, int partNumber, DateTimeOffset expiresAt, CancellationToken cancellationToken)
            => Task.FromResult(new Uri($"https://storage.test/{partNumber}"));

        public Task CompleteMultipartUploadAsync(string objectKey, string storageUploadId, IReadOnlyList<MediaUploadPart> parts, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task AbortMultipartUploadAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref abortCount);
            return Task.CompletedTask;
        }

        public Task DownloadObjectAsync(string objectKey, string destinationPath, CancellationToken cancellationToken)
            => Task.FromException(new NotSupportedException());

        public Task UploadDirectoryAsync(string sourceDirectory, string objectPrefix, CancellationToken cancellationToken)
            => Task.FromException(new NotSupportedException());

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
            => Task.FromException(new NotSupportedException());

        public Task DeletePrefixAsync(string objectPrefix, CancellationToken cancellationToken)
            => Task.FromException(new NotSupportedException());
    }

    private sealed class UnavailableMediaStoragePort : IMediaStoragePort
    {
        public Task<string> InitiateMultipartUploadAsync(string objectKey, string contentType, CancellationToken cancellationToken)
            => Task.FromException<string>(new StorageUnavailableException());

        public Task<IReadOnlyList<MediaUploadPart>> ListPartsAsync(string objectKey, string storageUploadId, CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<MediaUploadPart>>(new StorageUnavailableException());

        public Task<Uri> CreatePartUploadUriAsync(string objectKey, string storageUploadId, int partNumber, DateTimeOffset expiresAt, CancellationToken cancellationToken)
            => Task.FromException<Uri>(new StorageUnavailableException());

        public Task CompleteMultipartUploadAsync(string objectKey, string storageUploadId, IReadOnlyList<MediaUploadPart> parts, CancellationToken cancellationToken)
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
