using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class StudentSessionIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStudentSessionIdentityClient
{
    private const string CreateScope = "student-sessions:create";
    private const string ValidateScope = "student-sessions:validate";
    private const string RevokeScope = "student-sessions:revoke";

    public async Task<StudentSessionCreatedResult> CreateSessionAsync(
        StudentSessionLoginV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(
            "internal/v1/student-sessions",
            request,
            idempotencyKey,
            CreateScope);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<StudentSessionCreatedV1>(cancellationToken);
                return result is null
                    ? new StudentSessionCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE")
                    : new StudentSessionCreatedResult(200, null, result.SessionId, result.AccountId, result.Name, result.ExpiresAt);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "INVALID_CREDENTIALS")
            {
                return new StudentSessionCreatedResult(StatusCodes.Status401Unauthorized, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && code is "EMAIL_NOT_CONFIRMED" or "IDEMPOTENCY_CONFLICT")
            {
                return new StudentSessionCreatedResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StudentSessionCreatedResult(StatusCodes.Status400BadRequest, code);
            }

            return new StudentSessionCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentSessionCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentSessionCreatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentSessionCreatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentSessionCreatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
    }

    public async Task<StudentSessionValidatedResult> ValidateSessionAsync(
        Guid sessionId,
        string? audience,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(
            "internal/v1/student-session-validations",
            new StudentSessionValidationV1(sessionId, audience),
            null,
            ValidateScope);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<StudentSessionValidatedV1>(cancellationToken);
                return result is null
                    ? new StudentSessionValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE")
                    : new StudentSessionValidatedResult(200, null, result.AccountId, result.Name, result.ExpiresAt, result.AccessToken);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "SESSION_REQUIRED")
            {
                return new StudentSessionValidatedResult(StatusCodes.Status401Unauthorized, code);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden && code == "AUDIENCE_NOT_ALLOWED")
            {
                return new StudentSessionValidatedResult(StatusCodes.Status403Forbidden, code);
            }

            return new StudentSessionValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentSessionValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentSessionValidatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentSessionValidatedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentSessionValidatedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
    }

    public async Task<StudentSessionRevokedResult> RevokeSessionAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(
            "internal/v1/student-session-revocations",
            new StudentSessionReferenceV1(sessionId),
            idempotencyKey,
            RevokeScope);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new StudentSessionRevokedResult(StatusCodes.Status204NoContent, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            return response.StatusCode == HttpStatusCode.UnprocessableEntity && code == "IDEMPOTENCY_CONFLICT"
                ? new StudentSessionRevokedResult(StatusCodes.Status422UnprocessableEntity, code)
                : new StudentSessionRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentSessionRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentSessionRevokedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentSessionRevokedResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentSessionRevokedResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
    }

    private HttpRequestMessage CreateMessage<TRequest>(
        string path,
        TRequest request,
        string? idempotencyKey,
        string scope)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(scope));
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }

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
