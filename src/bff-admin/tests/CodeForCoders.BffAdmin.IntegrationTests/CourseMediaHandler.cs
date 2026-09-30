using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseMediaHandler : HttpMessageHandler
{
    private int concurrent;
    private int maximum;
    public ConcurrentQueue<(Uri Uri, string? Token)> Requests { get; } = new();
    public int MaxConcurrency => maximum;
    public string Title { get; set; } = "Current Media title";
    public string Status { get; set; } = "ready";
    public bool Malformed { get; set; }
    public bool Unavailable { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue((request.RequestUri!, request.Headers.Authorization?.Parameter));
        var active = Interlocked.Increment(ref concurrent);
        int previous;
        do { previous = maximum; } while (active > previous && Interlocked.CompareExchange(ref maximum, active, previous) != previous);
        try
        {
            await Task.Delay(Delay, cancellationToken);
            if (Unavailable) throw new HttpRequestException("Controlled Media outage.");
            if (Malformed) return new(HttpStatusCode.OK) { Content = new StringContent("invalid json") };
            var isList = request.RequestUri!.AbsolutePath == "/internal/v1/videos";
            var id = isList ? Guid.CreateVersion7() : Guid.Parse(request.RequestUri.Segments[^1]);
            var video = new
            {
                videoId = id,
                title = Title,
                status = Status,
                uploadedBy = new { accountId = Guid.CreateVersion7(), name = "School colleague" },
                uploadedAt = DateTimeOffset.UtcNow,
                durationSeconds = 125,
                failureReason = (string?)null
            };
            object payload = isList ? new { data = new[] { video }, pagination = new { page = 1, size = 20, total = 1, totalPages = 1 } } : video;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
        }
        finally { Interlocked.Decrement(ref concurrent); }
    }
}
