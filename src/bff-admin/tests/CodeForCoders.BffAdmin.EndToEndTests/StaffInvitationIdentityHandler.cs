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

    public StaffInvitationCreatedV1 Created { get; set; } = CreateInvitation();

    public StaffInvitationPageV1 Page { get; set; } = new([], new InvitationPaginationV1(1, 10, 0, 0));

    public Uri? LastRequestUri { get; private set; }

    public Guid? LastStaffSessionId { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastAssertionScope { get; private set; }

    public CreateStaffInvitationRequestV1? LastRequest { get; private set; }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        LastAssertionScope = ReadScope(request.Headers.Authorization?.Parameter);
        LastStaffSessionId = Guid.TryParse(request.Headers.GetValues("X-Staff-Session").SingleOrDefault(), out var sessionId)
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
        Created = CreateInvitation();
        Page = new StaffInvitationPageV1([], new InvitationPaginationV1(1, 10, 0, 0));
        LastRequestUri = null;
        LastStaffSessionId = null;
        LastIdempotencyKey = null;
        LastAssertionScope = null;
        LastRequest = null;
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
