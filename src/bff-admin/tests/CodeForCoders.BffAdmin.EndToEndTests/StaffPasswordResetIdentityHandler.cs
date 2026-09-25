using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class StaffPasswordResetIdentityHandler : HttpMessageHandler
{
    public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.NoContent;

    public string? ResponseCode { get; set; }

    public Uri? LastRequestUri { get; private set; }

    public StaffPasswordResetRequestV1? LastRequest { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastAssertion { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastIdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
        LastAssertion = request.Headers.Authorization?.Parameter;
        var content = await request.Content!.ReadAsStringAsync(cancellationToken);
        LastRequest = JsonSerializer.Deserialize<StaffPasswordResetRequestV1>(
            content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var response = new HttpResponseMessage(ResponseStatus);
        if (ResponseStatus != HttpStatusCode.NoContent && ResponseCode is not null)
        {
            response.Content = JsonContent.Create(new { code = ResponseCode });
        }

        return response;
    }

    public void Reset()
    {
        ResponseStatus = HttpStatusCode.NoContent;
        ResponseCode = null;
        LastRequestUri = null;
        LastRequest = null;
        LastIdempotencyKey = null;
        LastAssertion = null;
    }
}
