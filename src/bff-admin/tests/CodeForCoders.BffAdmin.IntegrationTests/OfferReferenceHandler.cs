using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class OfferReferenceHandler : HttpMessageHandler
{
    public List<Guid[]> Batches { get; } = [];
    public string? Token { get; private set; }
    public Uri? Uri { get; private set; }
    public string? Failure { get; set; }
    public bool Unknown { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Token = request.Headers.Authorization?.Parameter;
        Uri = request.RequestUri;
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var ids = body.GetProperty("offerIds").EnumerateArray().Select(id => id.GetGuid()).ToArray();
        Batches.Add(ids);
        if (Failure == "connection") throw new HttpRequestException("Unavailable.");
        if (Failure == "timeout") throw new TimeoutRejectedException("Timeout.");
        if (Failure == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("invalid json") };
        if (Failure == "shape") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = "invalid" }) };
        if (Failure == "503") return new(HttpStatusCode.ServiceUnavailable);
        if (Failure == "403") return new(HttpStatusCode.Forbidden);
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { data = Unknown ? [] : ids.Select(id => new { offerId = id, label = "Course — Option" }).ToArray() })
        };
    }
}
