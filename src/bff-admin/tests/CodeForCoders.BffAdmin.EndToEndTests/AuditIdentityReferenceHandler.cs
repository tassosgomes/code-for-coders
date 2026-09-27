using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class AuditIdentityReferenceHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? ProblemCode { get; set; }

    public int RequestCount { get; private set; }

    public string? AssertionScope { get; private set; }

    public Guid? StaffSessionId { get; private set; }

    public AuditIdentityReferenceLookupRequestV1? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        AssertionScope = ReadScope(request.Headers.Authorization?.Parameter);
        StaffSessionId = request.Headers.TryGetValues("X-Staff-Session", out var sessionIds)
            && Guid.TryParse(sessionIds.Single(), out var sessionId)
                ? sessionId
                : null;
        LastRequest = await request.Content!.ReadFromJsonAsync<AuditIdentityReferenceLookupRequestV1>(JsonOptions, cancellationToken);
        if (LastRequest is null
            || LastRequest.References.Any(reference => reference.Id == Guid.Empty
                || reference.Type is not ("conta-interna" or "convite-interno")))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new { code = "VALIDATION_FAILED" }, options: JsonOptions),
            };
        }

        if (StatusCode != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(StatusCode)
            {
                Content = JsonContent.Create(new { code = ProblemCode }, options: JsonOptions),
            };
        }

        var labels = LastRequest?.References.Select(reference => new AuditIdentityReferenceV1(
                reference.Type,
                reference.Id,
                reference.Id == AuditRecordSearchHandler.AuthorId ? "Marina Alves" : "Rafael Silva"))
            .ToArray() ?? [];
        return new HttpResponseMessage(StatusCode)
        {
            Content = JsonContent.Create(new AuditIdentityReferenceLookupResponseV1(labels), options: JsonOptions),
        };
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
        ProblemCode = null;
        RequestCount = 0;
        AssertionScope = null;
        StaffSessionId = null;
        LastRequest = null;
    }

    private static string? ReadScope(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var encodedPayload = parts[1].Replace('-', '+').Replace('_', '/');
        var payload = Convert.FromBase64String(encodedPayload + new string('=', (4 - encodedPayload.Length % 4) % 4));
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("scope").GetString();
    }
}
