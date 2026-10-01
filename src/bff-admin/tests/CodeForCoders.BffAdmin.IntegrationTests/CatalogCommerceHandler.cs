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
    public string? IdempotencyKey { get; private set; }
    public string? Body { get; private set; }
    public HttpMethod? Method { get; private set; }
    public string ErrorDetail { get; set; } = "tagline must contain between 1 and 160 characters.";
    public string ErrorCode { get; set; } = "FIELD_INVALID";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Token = request.Headers.Authorization?.Parameter; Uri = request.RequestUri;
        Method = request.Method;
        IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (Unavailable) throw new HttpRequestException("Commerce unavailable.");
        if (Timeout) throw new TimeoutRejectedException("Commerce timeout.");
        if (request.RequestUri!.AbsolutePath.Contains("/offers", StringComparison.Ordinal) && Status == HttpStatusCode.OK && !Malformed)
        {
            if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
            var publishing = request.RequestUri.AbsolutePath.EndsWith("/publish", StringComparison.Ordinal);
            var unpublishing = request.RequestUri.AbsolutePath.EndsWith("/unpublish", StringComparison.Ordinal);
            var transition = publishing || unpublishing;
            var courseId = request.Method == HttpMethod.Post && !transition ? Guid.Parse(request.RequestUri.Segments[^2].TrimEnd('/')) : Guid.CreateVersion7();
            return new HttpResponseMessage(request.Method == HttpMethod.Post && !transition ? HttpStatusCode.Created : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    offerId = Guid.CreateVersion7(),
                    courseId,
                    name = "Draft",
                    priceCents = 49700,
                    accessPeriod = new { type = "months", months = 12 },
                    status = publishing ? "published" : unpublishing ? "unpublished" : "draft",
                    purchaseIntentCount = 0,
                    createdAt = DateTimeOffset.UtcNow,
                    updatedAt = DateTimeOffset.UtcNow,
                    publishedAt = (DateTimeOffset?)null
                })
            };
        }
        HttpContent content = Malformed ? new StringContent("invalid json") : Status != HttpStatusCode.OK
            ? JsonContent.Create(new { code = Status == HttpStatusCode.Forbidden ? "PERMISSION_DENIED" : Status == HttpStatusCode.Unauthorized ? "TOKEN_INVALID" : ErrorCode, detail = ErrorDetail })
            : request.RequestUri!.AbsolutePath.EndsWith("/courses", StringComparison.Ordinal) ? JsonContent.Create(new
            {
                data = new[] { new { courseId = Guid.CreateVersion7(), title = "Catalog course", level = (string?)null, inShowcase = false,
                offerCounts = new { draft = 0, published = 0, unpublished = 0 } } },
                pagination = new { page = 2, size = 10, total = 11, totalPages = 2 }
            }) : JsonContent.Create(new
            {
                courseId = Guid.Parse(request.RequestUri.Segments[^1]),
                title = "Catalog course",
                level = (string?)null,
                prerequisite = new { text = (string?)null, recommendedCourses = Array.Empty<object>() },
                tagline = "Saved",
                inShowcase = false,
                offers = Array.Empty<object>()
            });
        return new HttpResponseMessage(Status) { Content = content };
    }
}
