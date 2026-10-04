using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyIdentityConfirmationHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string Response { get; set; } = "{\"eligible\":true}";
    public bool Timeout { get; set; }
    public bool Unavailable { get; set; }
    public string? Assertion { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Assertion = request.Headers.Authorization?.Parameter;
        if (Timeout) await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        if (Unavailable) throw new HttpRequestException("Controlled identity outage.");
        using var requestBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var body = JsonNode.Parse(Response)!.AsObject();
        body["studentId"] = requestBody.RootElement.GetProperty("studentId").GetGuid().ToString("D");
        return new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    }
}
