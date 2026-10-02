using System.Net;
using System.Net.Http.Json;
using Polly.Timeout;
namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyGrantHttpHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Key { get; private set; }
    public string? Token { get; private set; }
    public string? Body { get; private set; }
    public Uri? Uri { get; private set; }
    public int Status { get; set; } = 201;
    public string Code { get; set; } = "FIELD_INVALID";
    public bool Timeout { get; set; }
    public bool Unavailable { get; set; }
    public bool Malformed { get; set; }
    public Guid GrantId { get; } = Guid.CreateVersion7();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Token = request.Headers.Authorization?.Parameter; Key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken); Uri = request.RequestUri;
        if (Timeout) throw new TimeoutRejectedException(); if (Unavailable) throw new HttpRequestException("Controlled commerce outage.");
        return new((HttpStatusCode)Status)
        {
            Content = Malformed ? new StringContent("invalid") : Status >= 400 ? JsonContent.Create(new { code = Code })
            : request.RequestUri!.AbsolutePath.EndsWith("courtesy-term-preview", StringComparison.Ordinal) ? JsonContent.Create(new { months = 6, computedAt = "2026-10-15T14:00:00Z", endsOn = "2027-04-15", expiresAt = "2027-04-16T03:00:00Z" })
            : request.RequestUri!.AbsolutePath.EndsWith("access-grants", StringComparison.Ordinal) ? JsonContent.Create(new { data = new[] { new { grantId = GrantId, courseTitle = "Current course", origin = "purchase", status = "expired", endsOn = "2026-01-15", reason = (string?)null } }, pagination = new { page = 2, size = 1, total = 2, totalPages = 2 } })
            : JsonContent.Create(new { grantId = GrantId, studentId = Guid.CreateVersion7(), courseId = Guid.CreateVersion7(), courseTitle = "Course", origin = "courtesy", status = "active", accessPeriod = new { type = "months", months = 6 }, grantedAt = "2026-10-15T14:00:00Z", endsOn = "2027-04-15", expiresAt = "2027-04-16T03:00:00Z", reason = "Bolsa de mentoria" })
        };
    }
}
