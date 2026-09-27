using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class AuditRecordSearchHandler : HttpMessageHandler
{
    public static readonly Guid AuthorId = Guid.Parse("337fcd34-6bf6-4fe5-a1e6-608fe9426be7");

    public static readonly Guid TargetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    public static readonly Guid RecordId = Guid.Parse("5137eb89-3e71-4462-9c52-3994f7be0f9a");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public string? ProblemCode { get; set; }

    public int RequestCount { get; private set; }

    public string? AccessToken { get; private set; }

    public AuditRecordSearchRequestV1? LastRequest { get; private set; }

    public Guid? LastRecordId { get; private set; }

    public AuditRecordPageV1 Page { get; set; } = CreatePage();

    public AuditRecordDetailV1 Detail { get; set; } = CreateDetail();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        AccessToken = request.Headers.Authorization?.Parameter;
        if (request.Method == HttpMethod.Get)
        {
            var recordIdSegment = request.RequestUri?.Segments.LastOrDefault()?.Trim('/');
            LastRecordId = Guid.TryParse(recordIdSegment, out var recordId) ? recordId : null;
            return StatusCode == HttpStatusCode.OK
                ? new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(Detail, options: JsonOptions) }
                : new HttpResponseMessage(StatusCode) { Content = JsonContent.Create(new { code = ProblemCode }, options: JsonOptions) };
        }

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
        LastRecordId = null;
        Page = CreatePage();
        Detail = CreateDetail();
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

    private static AuditRecordDetailV1 CreateDetail()
        => new(
            RecordId,
            "papel-concedido",
            DateTimeOffset.Parse("2026-09-27T10:00:00Z"),
            new AuditRecordIdentityReferenceV1("conta-interna", AuthorId),
            new AuditRecordIdentityReferenceV1("conta-interna", TargetId),
            true,
            true,
            "identidade",
            DateTimeOffset.Parse("2026-09-27T10:00:04Z"),
            "Administrative reason",
            new Dictionary<string, string> { ["papel"] = "professor" },
            [],
            [new AuditRecordComplementV1(
                Guid.Parse("7137eb89-3e71-4462-9c52-3994f7be0f9a"),
                Guid.Parse("8137eb89-3e71-4462-9c52-3994f7be0f9a"),
                DateTimeOffset.Parse("2026-09-27T11:00:00Z"),
                new AuditRecordIdentityReferenceV1("conta-interna", TargetId),
                "Follow-up explanation")]);
}
