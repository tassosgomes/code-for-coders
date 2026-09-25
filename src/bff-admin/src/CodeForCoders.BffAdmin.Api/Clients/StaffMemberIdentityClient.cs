using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class StaffMemberIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStaffMemberIdentityClient
{
    private const string ReadScope = "staff-members:read";
    private const string WriteScope = "staff-members:write";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<StaffMemberIdentityResult> ListStaffMembersAsync(
        Guid identitySessionId,
        int page,
        int size,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Get,
            $"internal/v1/staff-members?page={page}&size={size}",
            ReadScope,
            identitySessionId,
            null,
            null,
            ReadPageAsync,
            cancellationToken);

    public Task<StaffMemberIdentityResult> GrantRoleAsync(
        Guid accountId,
        StaffRoleActionRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Post,
            $"internal/v1/staff-members/{accountId:D}/role-grants",
            WriteScope,
            identitySessionId,
            request,
            idempotencyKey,
            ReadActionAsync,
            cancellationToken);

    public Task<StaffMemberIdentityResult> RevokeRoleAsync(
        Guid accountId,
        StaffRoleActionRequestV1 request,
        Guid identitySessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            HttpMethod.Post,
            $"internal/v1/staff-members/{accountId:D}/role-revocations",
            WriteScope,
            identitySessionId,
            request,
            idempotencyKey,
            ReadActionAsync,
            cancellationToken);

    private async Task<StaffMemberIdentityResult> SendAsync(
        HttpMethod method,
        string path,
        string scope,
        Guid identitySessionId,
        object? body,
        string? idempotencyKey,
        Func<HttpResponseMessage, CancellationToken, Task<StaffMemberIdentityResult>> readSuccessAsync,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(scope));
        message.Headers.Add("X-Staff-Session", identitySessionId.ToString("D"));
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
            var statusCode = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST"
                || response.StatusCode == HttpStatusCode.Unauthorized && code == "SESSION_REQUIRED"
                || response.StatusCode == HttpStatusCode.Forbidden && code == "PERMISSION_DENIED"
                || response.StatusCode == HttpStatusCode.NotFound && code == "STAFF_MEMBER_NOT_FOUND"
                || response.StatusCode == HttpStatusCode.UnprocessableEntity
                    && code is "REASON_REQUIRED" or "SELF_ROLE_CHANGE_FORBIDDEN" or "ROLE_NOT_SUPPORTED" or "IDEMPOTENCY_CONFLICT")
            {
                return new StaffMemberIdentityResult(statusCode, code);
            }

            return Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }
        catch (TimeoutRejectedException)
        {
            return new StaffMemberIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffMemberIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
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

    private static StaffMemberIdentityResult Unavailable()
        => new(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");

    private static async Task<StaffMemberIdentityResult> ReadPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var page = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<StaffMemberPageV1>(JsonOptions, cancellationToken)
            : null;
        return page is null
            ? Unavailable()
            : new StaffMemberIdentityResult(StatusCodes.Status200OK, null, Page: page);
    }

    private static async Task<StaffMemberIdentityResult> ReadActionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var action = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<StaffRoleActionResultV1>(JsonOptions, cancellationToken)
            : null;
        return action is null
            ? Unavailable()
            : new StaffMemberIdentityResult(StatusCodes.Status200OK, null, Action: action);
    }
}
