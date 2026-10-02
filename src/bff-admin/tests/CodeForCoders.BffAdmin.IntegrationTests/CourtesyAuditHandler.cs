using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyAuditHandler : HttpMessageHandler
{
    public Guid RecordId { get; } = Guid.CreateVersion7();
    public Guid StudentId { get; } = Guid.CreateVersion7();
    public Guid CourseId { get; set; }
    public string? Token { get; private set; }
    public JsonElement? Search { get; private set; }
    public string Type { get; set; } = "cortesia-concedida";
    public string? CourseReference { get; set; }
    public Dictionary<string, string>? LastAttributes { get; private set; }
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Token = request.Headers.Authorization?.Parameter;
        var author = new AuditRecordIdentityReferenceV1("conta-interna", Guid.CreateVersion7());
        var target = new AuditRecordIdentityReferenceV1("conta-aluno", StudentId);
        object result;
        if (request.Method == HttpMethod.Get)
        {
            LastAttributes = new() { ["curso"] = CourseReference ?? CourseId.ToString("D"), ["vigencia"] = "6m", ["concessao"] = Guid.CreateVersion7().ToString("D") };
            result = new AuditRecordDetailV1(RecordId, Type, DateTimeOffset.UtcNow, author, target, true, false,
                "matricula", DateTimeOffset.UtcNow, "Bolsa integral do parceiro municipal", LastAttributes, [], []);
        }
        else
        {
            Search = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            result = new AuditRecordPageV1([new(RecordId, Type, DateTimeOffset.UtcNow, author, target, true, false)], new(1, 20, 1, 1, "snapshot"));
        }

        return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }
}
