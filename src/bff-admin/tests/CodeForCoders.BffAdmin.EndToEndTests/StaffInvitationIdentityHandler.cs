using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class StaffInvitationIdentityHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.Created;

    public string? CreateCode { get; set; }

    public HttpStatusCode ListStatus { get; set; } = HttpStatusCode.OK;

    public string? ListCode { get; set; }

    public HttpStatusCode LookupStatus { get; set; } = HttpStatusCode.OK;

    public string? LookupCode { get; set; }

    public HttpStatusCode AcceptStatus { get; set; } = HttpStatusCode.OK;

    public string? AcceptCode { get; set; }

    public StaffInvitationCreatedV1 Created { get; set; } = CreateInvitation();

    public StaffInvitationPageV1 Page { get; set; } = new([], new InvitationPaginationV1(1, 10, 0, 0));

    public StaffInvitationPreviewV1 Preview { get; set; } = new("professor", DateTimeOffset.UtcNow.AddDays(7));

    public StaffSessionCreatedV1 Session { get; set; } = CreateSession();

    public Uri? LastRequestUri { get; private set; }

    public Guid? LastStaffSessionId { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastAssertionScope { get; private set; }

    public CreateStaffInvitationRequestV1? LastRequest { get; private set; }

    public InvitationTokenRequestV1? LastLookupRequest { get; private set; }

    public AcceptStaffInvitationRequestV1? LastAcceptanceRequest { get; private set; }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        LastAssertionScope = ReadScope(request.Headers.Authorization?.Parameter);
        LastStaffSessionId = request.Headers.TryGetValues("X-Staff-Session", out var sessionValues)
            && Guid.TryParse(sessionValues.SingleOrDefault(), out var sessionId)
            ? sessionId
            : null;
        LastIdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys)
            ? keys.Single()
            : null;

        if (request.Method == HttpMethod.Get)
        {
            return ListStatus == HttpStatusCode.OK
                ? JsonResponse(ListStatus, Page)
                : ProblemResponse(ListStatus, ListCode);
        }

        if (request.RequestUri?.AbsolutePath.EndsWith("/internal/v1/staff-invitation-lookups", StringComparison.Ordinal) == true)
        {
            LastLookupRequest = await request.Content!.ReadFromJsonAsync<InvitationTokenRequestV1>(JsonOptions, cancellationToken);
            return LookupStatus == HttpStatusCode.OK
                ? JsonResponse(LookupStatus, Preview)
                : ProblemResponse(LookupStatus, LookupCode);
        }

        if (request.RequestUri?.AbsolutePath.EndsWith("/internal/v1/staff-invitation-acceptances", StringComparison.Ordinal) == true)
        {
            LastAcceptanceRequest = await request.Content!.ReadFromJsonAsync<AcceptStaffInvitationRequestV1>(JsonOptions, cancellationToken);
            return AcceptStatus == HttpStatusCode.OK
                ? JsonResponse(AcceptStatus, Session)
                : ProblemResponse(AcceptStatus, AcceptCode);
        }

        LastRequest = await request.Content!.ReadFromJsonAsync<CreateStaffInvitationRequestV1>(JsonOptions, cancellationToken);
        return CreateStatus == HttpStatusCode.Created
            ? JsonResponse(CreateStatus, Created)
            : ProblemResponse(CreateStatus, CreateCode);
    }

    public void Reset()
    {
        CreateStatus = HttpStatusCode.Created;
        CreateCode = null;
        ListStatus = HttpStatusCode.OK;
        ListCode = null;
        LookupStatus = HttpStatusCode.OK;
        LookupCode = null;
        AcceptStatus = HttpStatusCode.OK;
        AcceptCode = null;
        Created = CreateInvitation();
        Page = new StaffInvitationPageV1([], new InvitationPaginationV1(1, 10, 0, 0));
        Preview = new StaffInvitationPreviewV1("professor", DateTimeOffset.UtcNow.AddDays(7));
        Session = CreateSession();
        LastRequestUri = null;
        LastStaffSessionId = null;
        LastIdempotencyKey = null;
        LastAssertionScope = null;
        LastRequest = null;
        LastLookupRequest = null;
        LastAcceptanceRequest = null;
        RequestCount = 0;
    }

    private static StaffInvitationCreatedV1 CreateInvitation()
        => new(
            Guid.CreateVersion7(),
            "guest@example.com",
            "professor",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            null);

    private static StaffSessionCreatedV1 CreateSession()
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Marina Alves",
            ["professor"],
            ["autoria.ler"],
            DateTimeOffset.UtcNow.AddMinutes(60));

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T value)
        => new(statusCode) { Content = JsonContent.Create(value, options: JsonOptions) };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode statusCode, string? code)
        => new(statusCode) { Content = JsonContent.Create(new { code }) };

    private static string? ReadScope(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var segments = token.Split('.');
        if (segments.Length != 3)
        {
            return null;
        }

        var base64 = segments[1].Replace('-', '+').Replace('_', '/');
        using var document = JsonDocument.Parse(Convert.FromBase64String(base64 + new string('=', (4 - base64.Length % 4) % 4)));
        return document.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null;
    }
}
