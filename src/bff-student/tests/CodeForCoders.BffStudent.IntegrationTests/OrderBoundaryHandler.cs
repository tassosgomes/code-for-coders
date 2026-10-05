using System.Net;
using System.Text;
namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class OrderBoundaryHandler : HttpMessageHandler
{
    public int StatusCode { get; set; } = 200;
    public string Body { get; set; } = "{}";
    public string? Token { get; private set; }
    public string? Path { get; private set; }
    public string? Key { get; private set; }
    public string? RequestBody { get; private set; }
    public bool Timeout { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Path = request.RequestUri?.AbsolutePath; Token = request.Headers.Authorization?.Parameter;
        Key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (Timeout) throw new TaskCanceledException("Controlled timeout");
        return new((HttpStatusCode)StatusCode) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
    }
}
