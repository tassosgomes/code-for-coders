using System.Net;
using System.Net.Http.Json;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class VideoUploadHandler : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? ProblemCode { get; set; }

    public int RequestCount { get; private set; }

    public string? LastAccessToken { get; private set; }

    public string? LastPath { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastAccessToken = request.Headers.Authorization?.Parameter;
        LastPath = request.RequestUri?.PathAndQuery;
        LastIdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var values)
            ? values.Single()
            : null;
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if ((int)StatusCode >= 400)
        {
            return new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    type = "about:blank",
                    title = "Storage unavailable",
                    status = (int)StatusCode,
                    code = ProblemCode,
                    traceId = "video-upload-test",
                }),
            };
        }

        if (request.RequestUri?.AbsolutePath.EndsWith("/complete", StringComparison.Ordinal) == true)
        {
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    videoId = Guid.CreateVersion7(),
                    title = "Aula de teste",
                    status = "received",
                    uploadedBy = new { accountId = Guid.CreateVersion7(), name = "Marina Alves" },
                    uploadedAt = DateTimeOffset.UtcNow,
                    durationSeconds = (int?)null,
                    failureReason = (string?)null,
                }),
            };
        }

        if (request.RequestUri?.AbsolutePath.EndsWith("/part-urls", StringComparison.Ordinal) == true)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    parts = new[] { new { partNumber = 1, url = "http://storage.test/p/part-1", expiresAt = DateTimeOffset.UtcNow.AddMinutes(60) } },
                    uploadExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
                }),
            };
        }

        if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/internal/v1/video-uploads")
        {
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    uploadId = Guid.CreateVersion7(),
                    title = "Aula de teste",
                    fileName = "aula.mp4",
                    fileSize = 3,
                    partSize = 67108864,
                    partCount = 1,
                    receivedParts = Array.Empty<int>(),
                    expiresAt = DateTimeOffset.UtcNow.AddHours(24),
                }),
            };
        }

        if (request.Method == HttpMethod.Get
            && request.RequestUri?.AbsolutePath == "/internal/v1/video-uploads")
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = Array.Empty<object>(),
                    pagination = new { page = 1, size = 10, total = 0, totalPages = 0 },
                }),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                uploadId = Guid.CreateVersion7(),
                title = "Aula de teste",
                fileName = "aula.mp4",
                fileSize = 3,
                partSize = 67108864,
                partCount = 1,
                receivedParts = Array.Empty<int>(),
                expiresAt = DateTimeOffset.UtcNow.AddHours(24),
            }),
        };
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
        ProblemCode = null;
        RequestCount = 0;
        LastAccessToken = null;
        LastPath = null;
        LastIdempotencyKey = null;
        LastBody = null;
    }
}
