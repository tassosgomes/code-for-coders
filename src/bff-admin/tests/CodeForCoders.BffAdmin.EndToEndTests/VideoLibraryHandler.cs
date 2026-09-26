using System.Net;
using System.Net.Http.Json;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class VideoLibraryHandler : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? ProblemCode { get; set; }

    public int RequestCount { get; private set; }

    public string? LastAccessToken { get; private set; }

    public string? LastPath { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastAccessToken = request.Headers.Authorization?.Parameter;
        LastPath = request.RequestUri?.PathAndQuery;

        if (StatusCode == HttpStatusCode.OK && request.RequestUri?.AbsolutePath.EndsWith("/videos", StringComparison.Ordinal) == true)
        {
            return Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    data = Array.Empty<object>(),
                    pagination = new { page = 1, size = 10, total = 0, totalPages = 0 },
                }),
            });
        }

        if ((int)StatusCode < 400)
        {
            return Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    videoId = Guid.CreateVersion7(),
                    title = "Aula 1",
                    status = "received",
                    uploadedBy = new { accountId = Guid.CreateVersion7(), name = "Marina Alves" },
                    uploadedAt = DateTimeOffset.UtcNow,
                    durationSeconds = (int?)null,
                    failureReason = (string?)null,
                }),
            });
        }

        return Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = JsonContent.Create(new
            {
                type = "about:blank",
                title = "Media unavailable",
                status = (int)StatusCode,
                code = ProblemCode,
                traceId = "video-library-test",
            }),
        });
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
        ProblemCode = null;
        RequestCount = 0;
        LastAccessToken = null;
        LastPath = null;
    }
}
