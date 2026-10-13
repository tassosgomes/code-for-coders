using System.Net;
using System.Net.Sockets;
using System.Text;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class MediaStorageBoundaryTests
{
    [Fact(DisplayName = nameof(S3AdapterSignsPartUrlAgainstPublicPathStyleEndpoint))]
    [Trait("Layer", "Media storage boundary - Unit")]
    public async Task S3AdapterSignsPartUrlAgainstPublicPathStyleEndpoint()
    {
        var options = Options.Create(new AwsMediaOptions
        {
            Region = "us-east-1",
            BucketName = "foundation-media",
            ObjectKeyPrefix = "objects",
            EndpointInternal = "http://minio:9000",
            EndpointPublic = "http://localhost:9000",
            AccessKeyId = "test-access",
            SecretAccessKey = "test-secret-key",
            ForcePathStyle = true,
        });
        using var clients = new S3MediaClientPair(options);
        var storage = new S3MediaStorageAdapter(clients, options);

        var url = await storage.CreatePartUploadUriAsync(
            "tenant/video/original",
            "provider-upload-id",
            2,
            DateTimeOffset.UtcNow.AddMinutes(60),
            TestContext.Current.CancellationToken);

        Assert.Equal("http", url.Scheme);
        Assert.Equal("localhost:9000", url.Authority);
        Assert.Equal("/foundation-media/objects/tenant/video/original", url.AbsolutePath);
        Assert.Contains("partNumber=2", url.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uploadId=provider-upload-id", url.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Amz-Signature", url.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(VideoUploadKeepsGeneratedObjectKeyIndependentFromFileMetadata))]
    [Trait("Layer", "Media storage boundary - Unit")]
    public void VideoUploadKeepsGeneratedObjectKeyIndependentFromFileMetadata()
    {
        var tenantId = Guid.CreateVersion7();
        var upload = VideoUpload.Create(
            tenantId,
            Guid.CreateVersion7(),
            "Aula de introdução",
            "nome-de-pessoa.mp4",
            (2 * VideoUpload.PartSizeBytes) + 1,
            "video/mp4",
            "fingerprint-value-123",
            "Nome da pessoa",
            DateTimeOffset.UtcNow);

        Assert.Equal(3, upload.PartCount);
        Assert.StartsWith($"{tenantId:D}/", upload.ObjectKey, StringComparison.Ordinal);
        Assert.EndsWith("/original", upload.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain("nome-de-pessoa", upload.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain("Aula de introdução", upload.ObjectKey, StringComparison.Ordinal);
        Assert.DoesNotContain("Nome da pessoa", upload.ObjectKey, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(S3AdapterKeepsRejectedCredentialCauseWhenStorageIsUnavailable))]
    [Trait("Layer", "Media storage boundary - Unit")]
    public async Task S3AdapterKeepsRejectedCredentialCauseWhenStorageIsUnavailable()
    {
        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var server = RespondSignatureDoesNotMatchAsync(listener);
        var options = Options.Create(new AwsMediaOptions
        {
            Region = "us-east-1",
            BucketName = "foundation-media",
            ObjectKeyPrefix = "objects",
            EndpointInternal = $"http://127.0.0.1:{port}",
            EndpointPublic = "http://localhost:9000",
            AccessKeyId = "test-access",
            SecretAccessKey = "test-secret-key",
            ForcePathStyle = true,
        });
        using var clients = new S3MediaClientPair(options);
        var storage = new S3MediaStorageAdapter(clients, options);

        var exception = await Assert.ThrowsAsync<StorageUnavailableException>(
            () => storage.ReadPlaylistAsync("tenant/video/hls/master.m3u8", TestContext.Current.CancellationToken));
        await server;

        Assert.Contains("S3 403 SignatureDoesNotMatch", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
        Assert.DoesNotContain("test-secret-key", exception.ToString(), StringComparison.Ordinal);
    }

    private static int GetFreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private static async Task RespondSignatureDoesNotMatchAsync(HttpListener listener)
    {
        var context = await listener.GetContextAsync();
        var body = Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>SignatureDoesNotMatch</Code>"
            + "<Message>The request signature we calculated does not match the signature you provided.</Message></Error>");
        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        context.Response.ContentType = "application/xml";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }
}
