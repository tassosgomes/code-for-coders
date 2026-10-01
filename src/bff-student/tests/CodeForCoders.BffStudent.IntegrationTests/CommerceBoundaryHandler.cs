using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed record CapturedCommerceRequest(HttpMethod Method, Uri? RequestUri, string? Authorization, bool HasCookie);

/// <summary>Controlled boundary of the test: the real BFF HTTP client talks to this handler instead of <c>commerce</c>.</summary>
public sealed class CommerceBoundaryHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<CapturedCommerceRequest> requests = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(HttpStatusCode.OK, EmptyPage);

    public IReadOnlyList<CapturedCommerceRequest> Requests => [.. requests];

    public static string EmptyPage => """{"data":[],"pagination":{"page":1,"size":12,"total":0,"totalPages":0}}""";

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
        => new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Enqueue(new CapturedCommerceRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Headers.Contains("Cookie")));
        return Task.FromResult(Respond(request));
    }
}
