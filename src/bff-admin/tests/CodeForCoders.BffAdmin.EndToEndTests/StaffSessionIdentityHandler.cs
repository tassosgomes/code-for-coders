using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class StaffSessionIdentityHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.OK;

    public string? CreateCode { get; set; }

    public HttpStatusCode ValidateStatus { get; set; } = HttpStatusCode.OK;

    public string? ValidateCode { get; set; }

    public HttpStatusCode RevokeStatus { get; set; } = HttpStatusCode.NoContent;

    public string? RevokeCode { get; set; }

    public StaffSessionCreatedV1 CreatedSession { get; set; } = CreateSession();

    public StaffSessionValidatedV1 ValidatedSession { get; set; } = CreateValidatedSession();

    public Uri? LastRequestUri { get; private set; }

    public StaffSessionLoginV1? LastLoginRequest { get; private set; }

    public string? LastIdempotencyKey { get; private set; }

    public string? LastAssertionScope { get; private set; }

    public string? LastValidationAudience { get; private set; }

    public int ValidationCount { get; private set; }

    public int RevocationCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastAssertionScope = ReadScope(request.Headers.Authorization?.Parameter);
        if (request.Headers.TryGetValues("Idempotency-Key", out var keys))
        {
            LastIdempotencyKey = keys.Single();
        }

        var path = request.RequestUri?.AbsolutePath;
        switch (path)
        {
            case "/internal/v1/staff-sessions":
                LastLoginRequest = await request.Content!.ReadFromJsonAsync<StaffSessionLoginV1>(JsonOptions, cancellationToken);
                return CreateStatus == HttpStatusCode.OK
                    ? JsonResponse(CreateStatus, CreatedSession)
                    : ProblemResponse(CreateStatus, CreateCode);
            case "/internal/v1/staff-session-validations":
                ValidationCount++;
                var validation = await request.Content!.ReadFromJsonAsync<StaffSessionValidationV1>(JsonOptions, cancellationToken);
                LastValidationAudience = validation?.Audience;
                return ValidateStatus == HttpStatusCode.OK
                    ? JsonResponse(ValidateStatus, ValidatedSession)
                    : ProblemResponse(ValidateStatus, ValidateCode);
            case "/internal/v1/staff-session-revocations":
                RevocationCount++;
                return RevokeStatus == HttpStatusCode.NoContent
                    ? new HttpResponseMessage(RevokeStatus)
                    : ProblemResponse(RevokeStatus, RevokeCode);
            case "/internal/v1/staff-password-resets":
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    public void Reset()
    {
        CreateStatus = HttpStatusCode.OK;
        CreateCode = null;
        ValidateStatus = HttpStatusCode.OK;
        ValidateCode = null;
        RevokeStatus = HttpStatusCode.NoContent;
        RevokeCode = null;
        CreatedSession = CreateSession();
        ValidatedSession = CreateValidatedSession();
        LastRequestUri = null;
        LastLoginRequest = null;
        LastIdempotencyKey = null;
        LastAssertionScope = null;
        LastValidationAudience = null;
        ValidationCount = 0;
        RevocationCount = 0;
    }

    private static StaffSessionCreatedV1 CreateSession()
        => new(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Marina Alves",
            ["administrador"],
            ["acesso.gerir"],
            DateTimeOffset.UtcNow.AddMinutes(30));

    private static StaffSessionValidatedV1 CreateValidatedSession()
        => new(
            Guid.CreateVersion7(),
            "Marina Alves",
            ["administrador"],
            ["acesso.gerir"],
            DateTimeOffset.UtcNow.AddMinutes(30),
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
