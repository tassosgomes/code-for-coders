using System.Net;
using System.Net.Http.Json;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CatalogCommerceHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Token { get; private set; }
    public Uri? Uri { get; private set; }
    public bool Unavailable { get; set; }
    public bool Timeout { get; set; }
    public bool Malformed { get; set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Token = request.Headers.Authorization?.Parameter; Uri = request.RequestUri;
        if (Unavailable) throw new HttpRequestException("Commerce unavailable.");
        if (Timeout) throw new TimeoutRejectedException("Commerce timeout.");
        HttpContent content = Malformed ? new StringContent("invalid json") : Status != HttpStatusCode.OK
            ? JsonContent.Create(new { code = Status == HttpStatusCode.Forbidden ? "PERMISSION_DENIED" : "TOKEN_INVALID" })
            : JsonContent.Create(new
            {
                data = new[] { new { courseId = Guid.CreateVersion7(), title = "Catalog course", level = (string?)null, inShowcase = false,
                offerCounts = new { draft = 0, published = 0, unpublished = 0 } } },
                pagination = new { page = 2, size = 10, total = 11, totalPages = 2 }
            });
        return Task.FromResult(new HttpResponseMessage(Status) { Content = content });
    }
}
