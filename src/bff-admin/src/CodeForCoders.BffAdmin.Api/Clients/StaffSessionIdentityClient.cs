using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class StaffSessionIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStaffSessionIdentityClient
{
    private const string CreateScope = "staff-sessions:create";
    private const string ValidateScope = "staff-sessions:validate";
    private const string RevokeScope = "staff-sessions:revoke";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StaffSessionIdentityCreatedResult> CreateSessionAsync(
        StaffSessionLoginV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            "internal/v1/staff-sessions",
            CreateScope,
            request);
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var session = await response.Content.ReadFromJsonAsync<StaffSessionCreatedV1>(JsonOptions, cancellationToken);
                return session is null
                    ? new StaffSessionIdentityCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null)
                    : new StaffSessionIdentityCreatedResult(StatusCodes.Status200OK, null, session);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "INVALID_CREDENTIALS")
            {
                return new StaffSessionIdentityCreatedResult(StatusCodes.Status401Unauthorized, code, null);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StaffSessionIdentityCreatedResult(StatusCodes.Status400BadRequest, code, null);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code == "IDEMPOTENCY_CONFLICT")
            {
                return new StaffSessionIdentityCreatedResult(StatusCodes.Status422UnprocessableEntity, code, null);
            }

            return new StaffSessionIdentityCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (HttpRequestException)
        {
            return new StaffSessionIdentityCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (JsonException)
        {
            return new StaffSessionIdentityCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (TimeoutRejectedException)
        {
            return new StaffSessionIdentityCreatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
        catch (ExecutionRejectedException)
        {
            return new StaffSessionIdentityCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffSessionIdentityCreatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
    }

    public async Task<StaffSessionIdentityValidatedResult> ValidateSessionAsync(
        Guid sessionId,
        string? audience,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            "internal/v1/staff-session-validations",
            ValidateScope,
            new StaffSessionValidationV1(sessionId, audience));

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var session = await response.Content.ReadFromJsonAsync<StaffSessionValidatedV1>(JsonOptions, cancellationToken);
                return session is null
                    ? new StaffSessionIdentityValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null)
                    : new StaffSessionIdentityValidatedResult(StatusCodes.Status200OK, null, session);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "SESSION_REQUIRED")
            {
                return new StaffSessionIdentityValidatedResult(StatusCodes.Status401Unauthorized, code, null);
            }

            return new StaffSessionIdentityValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (HttpRequestException)
        {
            return new StaffSessionIdentityValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (JsonException)
        {
            return new StaffSessionIdentityValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (TimeoutRejectedException)
        {
            return new StaffSessionIdentityValidatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
        catch (ExecutionRejectedException)
        {
            return new StaffSessionIdentityValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE", null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffSessionIdentityValidatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE", null);
        }
    }

    public async Task<StaffSessionIdentityRevokedResult> RevokeSessionAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Post,
            "internal/v1/staff-session-revocations",
            RevokeScope,
            new StaffSessionReferenceV1(sessionId));
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new StaffSessionIdentityRevokedResult(StatusCodes.Status204NoContent, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code == "IDEMPOTENCY_CONFLICT")
            {
                return new StaffSessionIdentityRevokedResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StaffSessionIdentityRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StaffSessionIdentityRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StaffSessionIdentityRevokedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StaffSessionIdentityRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffSessionIdentityRevokedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
    }

    private HttpRequestMessage CreateRequest<TRequest>(
        HttpMethod method,
        string path,
        string scope,
        TRequest request)
    {
        var message = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(scope));
        return message;
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
}
