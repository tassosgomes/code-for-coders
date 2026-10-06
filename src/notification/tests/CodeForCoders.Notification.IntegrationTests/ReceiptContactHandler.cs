using System.Collections.Concurrent;

namespace CodeForCoders.Notification.IntegrationTests;

internal sealed class ReceiptContactHandler : HttpMessageHandler
{
    public ConcurrentQueue<string> Tokens { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = null!;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.Parameter is { } token) Tokens.Enqueue(token);
        return Task.FromResult(Response(request));
    }
}
