using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseAuditHandler : HttpMessageHandler
{
    public Guid CourseId { get; set; }
    public string TargetType { get; set; } = "curso";
    public Guid[]? ListTargets { get; set; }
    public Guid RecordId { get; } = Guid.CreateVersion7();
    public string? Token { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Token = request.Headers.Authorization?.Parameter;
        var target = new AuditRecordIdentityReferenceV1(TargetType, CourseId);
        object response = request.Method == HttpMethod.Get
            ? new AuditRecordDetailV1(RecordId, "versao-publicada", DateTimeOffset.UtcNow, null, target, true, false, "conteudo", DateTimeOffset.UtcNow, null, new Dictionary<string, string> { ["versao"] = "1" }, [], [])
            : new AuditRecordPageV1((ListTargets ?? [CourseId]).Select(id => new AuditRecordSummaryV1(RecordId,
                TargetType == "oferta" ? "oferta-publicada" : "versao-publicada", DateTimeOffset.UtcNow, null,
                new AuditRecordIdentityReferenceV1(TargetType, id), true, false)).ToArray(),
                new(1, 20, ListTargets?.Length ?? 1, 1, "snapshot"));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) });
    }
}
