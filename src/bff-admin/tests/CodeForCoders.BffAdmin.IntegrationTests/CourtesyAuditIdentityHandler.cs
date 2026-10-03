using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyAuditIdentityHandler : HttpMessageHandler
{
    public string[] Roles { get; set; } = ["administrador"];
    public Guid ExpectedSessionId { get; set; }
    public Guid? ReceivedSessionId { get; private set; }
    public string? AssertionScope { get; private set; }
    public bool Unresolved { get; set; }
    public HttpStatusCode ReferenceStatus { get; set; } = HttpStatusCode.OK;
    public List<AuditIdentityReferenceLookupItemV1> References { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath == "/internal/v1/audit-identity-reference-lookups")
        {
            ReceivedSessionId = Guid.Parse(request.Headers.GetValues("X-Staff-Session").Single());
            var payload = request.Headers.Authorization!.Parameter!.Split('.')[1].Replace('-', '+').Replace('_', '/');
            using var claims = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')));
            AssertionScope = claims.RootElement.GetProperty("scope").GetString();
            var body = (await request.Content!.ReadFromJsonAsync<AuditIdentityReferenceLookupRequestV1>(cancellationToken))!;
            References.AddRange(body.References);
            if (ReferenceStatus != HttpStatusCode.OK)
                return new(ReferenceStatus) { Content = JsonContent.Create(new { code = ReferenceStatus == HttpStatusCode.Forbidden ? "PERMISSION_DENIED" : "IDENTITY_UNAVAILABLE" }) };
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AuditIdentityReferenceLookupResponseV1(body.References.Select(item =>
                    new AuditIdentityReferenceV1(item.Type, item.Id,
                        item.Type == "conta-aluno" ? Unresolved ? null : "Joana Ribeiro" : "Marina Costa")).ToArray())),
            };
        }

        var input = (await request.Content!.ReadFromJsonAsync<StaffSessionValidationV1>(cancellationToken))!;
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StaffSessionValidatedV1(Guid.CreateVersion7(), "Marina Costa", Roles, [],
                DateTimeOffset.UtcNow.AddHours(1), input.Audience == "learning" ? "server-learning-token" : input.Audience == "audit" ? "server-audit-token" : null)),
        };
    }
}
