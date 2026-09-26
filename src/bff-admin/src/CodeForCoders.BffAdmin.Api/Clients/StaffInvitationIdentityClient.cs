using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class StaffInvitationIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStaffInvitationIdentityClient
{
    private const string ReadScope = "staff-invitations:read";
    private const string WriteScope = "staff-invitations:write";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<StaffInvitationIdentityResult> CreateInvitationAsync(
        CreateStaffInvitationRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Post,
            "internal/v1/staff-invitations",
            WriteScope,
            identitySessionId,
            request,
            idempotencyKey,
            ReadCreatedAsync,
            cancellationToken);

    public Task<StaffInvitationIdentityResult> ListPendingInvitationsAsync(
        Guid identitySessionId,
        int page,
        int size,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Get,
            $"internal/v1/staff-invitations?page={page}&size={size}",
            ReadScope,
            identitySessionId,
            null,
            null,
            ReadPageAsync,
            cancellationToken);

    public Task<StaffInvitationIdentityResult> LookupInvitationAsync(
        InvitationTokenRequestV1 request,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Post,
            "internal/v1/staff-invitation-lookups",
            ReadScope,
            null,
            request,
            null,
            ReadPreviewAsync,
            cancellationToken);

    public Task<StaffInvitationIdentityResult> AcceptInvitationAsync(
        AcceptStaffInvitationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Post,
            "internal/v1/staff-invitation-acceptances",
            WriteScope,
            null,
            request,
            idempotencyKey,
            ReadSessionAsync,
            cancellationToken);

    private async Task<StaffInvitationIdentityResult> SendAsync(
        HttpMethod method,
        string path,
        string scope,
        Guid? identitySessionId,
        object? body,
        string? idempotencyKey,
        Func<HttpResponseMessage, CancellationToken, Task<StaffInvitationIdentityResult>> readSuccessAsync,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            assertionTokenFactory.Create(scope));
        if (identitySessionId is Guid sessionId)
        {
            message.Headers.Add("X-Staff-Session", sessionId.ToString("D"));
        }

        if (idempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await readSuccessAsync(response, cancellationToken);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StaffInvitationIdentityResult(StatusCodes.Status400BadRequest, code, null, null);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "SESSION_REQUIRED")
            {
                return new StaffInvitationIdentityResult(StatusCodes.Status401Unauthorized, code, null, null);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED")
            {
                return new StaffInvitationIdentityResult(StatusCodes.Status403Forbidden, code, null, null);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && code is "EMAIL_BELONGS_TO_STAFF" or "EMAIL_BELONGS_TO_STUDENT" or "REASON_REQUIRED"
                    or "ROLE_NOT_SUPPORTED" or "IDEMPOTENCY_CONFLICT" or "INVITATION_INVALID"
                    or "INVITATION_EMAIL_UNAVAILABLE" or "PASSWORD_POLICY_VIOLATION")
            {
                return new StaffInvitationIdentityResult(StatusCodes.Status422UnprocessableEntity, code, null, null);
            }

            return Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }
        catch (TimeoutRejectedException)
        {
            return new StaffInvitationIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null, null);
        }
        catch (ExecutionRejectedException)
        {
            return Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffInvitationIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null, null);
        }
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("code", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static StaffInvitationIdentityResult Unavailable()
        => new(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null, null);

    private static async Task<StaffInvitationIdentityResult> ReadCreatedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.Created)
        {
            return Unavailable();
        }

        var created = await response.Content.ReadFromJsonAsync<StaffInvitationCreatedV1>(JsonOptions, cancellationToken);
        return created is null
            ? Unavailable()
            : new StaffInvitationIdentityResult(StatusCodes.Status201Created, null, created, null);
    }

    private static async Task<StaffInvitationIdentityResult> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<StaffInvitationPageV1>(JsonOptions, cancellationToken)
            : null;
        return page is null
            ? Unavailable()
            : new StaffInvitationIdentityResult(StatusCodes.Status200OK, null, null, page);
    }

    private static async Task<StaffInvitationIdentityResult> ReadPreviewAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var preview = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<StaffInvitationPreviewV1>(JsonOptions, cancellationToken)
            : null;
        return preview is null
            ? Unavailable()
            : new StaffInvitationIdentityResult(StatusCodes.Status200OK, null, null, null, preview);
    }

    private static async Task<StaffInvitationIdentityResult> ReadSessionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var session = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<StaffSessionCreatedV1>(JsonOptions, cancellationToken)
            : null;
        return session is null
            ? Unavailable()
            : new StaffInvitationIdentityResult(StatusCodes.Status200OK, null, null, null, null, session);
    }
}
