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

    public HttpMethod? LastMethod { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastBody { get; private set; }

    public string[] Tags { get; set; } = [];

    public object? TranscriptionState { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        LastAccessToken = request.Headers.Authorization?.Parameter;
        LastPath = request.RequestUri?.PathAndQuery;
        LastMethod = request.Method;
        LastIdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.FirstOrDefault() : null;

        if (StatusCode == HttpStatusCode.OK && request.RequestUri?.AbsolutePath == "/internal/v1/video-tags")
            return new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(new { data = Tags }) };

        if (StatusCode == HttpStatusCode.OK && request.RequestUri?.AbsolutePath.EndsWith("/videos", StringComparison.Ordinal) == true)
        {
            return new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    data = Array.Empty<object>(),
                    pagination = new { page = 1, size = 10, total = 0, totalPages = 0 },
                }),
            };
        }

        if ((int)StatusCode < 400)
        {
            return new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new
                {
                    videoId = Guid.CreateVersion7(),
                    title = "Aula 1",
                    status = StatusCode == HttpStatusCode.Accepted ? "ready" : "received",
                    uploadedBy = new { accountId = Guid.CreateVersion7(), name = "Marina Alves" },
                    uploadedAt = DateTimeOffset.UtcNow,
                    durationSeconds = (int?)null,
                    failureReason = (string?)null,
                    tags = Tags,
                    transcription = TranscriptionState ?? new { status = StatusCode == HttpStatusCode.Accepted ? "in-progress" : "not-requested", readable = false, readyAt = (DateTimeOffset?)null, failureReason = (string?)null, canStart = false },
                }),
            };
        }

        return new HttpResponseMessage(StatusCode)
        {
            Content = JsonContent.Create(new
            {
                type = "about:blank",
                title = "Media unavailable",
                status = (int)StatusCode,
                code = ProblemCode,
                traceId = "video-library-test",
            }),
        };
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
        LastBody = null;
        Tags = [];
        TranscriptionState = null;
        ProblemCode = null;
        RequestCount = 0;
        LastAccessToken = null;
        LastPath = null;
        LastMethod = null;
        LastIdempotencyKey = null;
    }
}
