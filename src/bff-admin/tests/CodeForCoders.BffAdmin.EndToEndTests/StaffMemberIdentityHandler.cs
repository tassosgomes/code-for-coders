using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class StaffMemberIdentityHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode ListStatus { get; set; } = HttpStatusCode.OK;

    public string? ListCode { get; set; }

    public HttpStatusCode ActionStatus { get; set; } = HttpStatusCode.OK;

    public string? ActionCode { get; set; }

    public StaffMemberPageV1 Page { get; set; } = new([], new StaffMemberPaginationV1(1, 10, 0, 0));

    public StaffRoleActionResultV1 Action { get; set; } = CreateAction();

    public Uri? LastRequestUri { get; private set; }

    public Guid? LastStaffSessionId { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastAssertionScope { get; private set; }

    public StaffRoleActionRequestV1? LastRequest { get; private set; }

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

        LastRequest = await request.Content!.ReadFromJsonAsync<StaffRoleActionRequestV1>(JsonOptions, cancellationToken);
        return ActionStatus == HttpStatusCode.OK
            ? JsonResponse(ActionStatus, Action)
            : ProblemResponse(ActionStatus, ActionCode);
    }

    public void Reset()
    {
        ListStatus = HttpStatusCode.OK;
        ListCode = null;
        ActionStatus = HttpStatusCode.OK;
        ActionCode = null;
        Page = new StaffMemberPageV1([], new StaffMemberPaginationV1(1, 10, 0, 0));
        Action = CreateAction();
        LastRequestUri = null;
        LastStaffSessionId = null;
        LastIdempotencyKey = null;
        LastAssertionScope = null;
        LastRequest = null;
        RequestCount = 0;
    }

    private static StaffRoleActionResultV1 CreateAction()
        => new(
            new StaffMemberV1(
                Guid.CreateVersion7(),
                "Marina Alves",
                "marina@example.com",
                ["professor"],
                false),
            true,
            false);

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

        var encoded = segments[1].Replace('-', '+').Replace('_', '/');
        using var document = JsonDocument.Parse(Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4)));
        return document.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null;
    }
}
