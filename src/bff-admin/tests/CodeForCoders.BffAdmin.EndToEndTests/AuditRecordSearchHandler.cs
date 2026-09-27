using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class AuditRecordSearchHandler : HttpMessageHandler
{
    public static readonly Guid AuthorId = Guid.Parse("337fcd34-6bf6-4fe5-a1e6-608fe9426be7");

    public static readonly Guid TargetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? ProblemCode { get; set; }

    public int RequestCount { get; private set; }

    public string? AccessToken { get; private set; }

    public AuditRecordSearchRequestV1? LastRequest { get; private set; }

    public AuditRecordPageV1 Page { get; set; } = CreatePage();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        AccessToken = request.Headers.Authorization?.Parameter;
        LastRequest = await request.Content!.ReadFromJsonAsync<AuditRecordSearchRequestV1>(JsonOptions, cancellationToken);
        return StatusCode == HttpStatusCode.OK
            ? new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(Page, options: JsonOptions) }
            : new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(new { code = ProblemCode }, options: JsonOptions) };
    }

    public void Reset()
    {
        StatusCode = HttpStatusCode.OK;
        ProblemCode = null;
        RequestCount = 0;
        AccessToken = null;
        LastRequest = null;
        Page = CreatePage();
    }

    private static AuditRecordPageV1 CreatePage()
        => new(
            [new AuditRecordSummaryV1(
                Guid.CreateVersion7(),
                "papel-concedido",
                DateTimeOffset.UtcNow,
                new AuditRecordIdentityReferenceV1("conta-interna", AuthorId),
                new AuditRecordIdentityReferenceV1("conta-interna", TargetId),
                true,
                false)],
            new AuditRecordPaginationV1(1, 20, 1, 1, "snap_7mQ2kV4b123456789012345678901234567890123"));
}
