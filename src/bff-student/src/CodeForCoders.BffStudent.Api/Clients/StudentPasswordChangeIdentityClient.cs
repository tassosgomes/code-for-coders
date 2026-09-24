using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class StudentPasswordChangeIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStudentPasswordChangeIdentityClient
{
    private const string ChangeScope = "student-password-changes:execute";

    public async Task<StudentPasswordChangeResult> ChangePasswordAsync(
        Guid sessionId,
        StudentPasswordChangeV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/password-changes")
        {
            Content = JsonContent.Create(new IdentityPasswordChangeRequest(
                sessionId,
                request.CurrentPassword,
                request.NewPassword)),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(ChangeScope));
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new StudentPasswordChangeResult(StatusCodes.Status204NoContent, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StudentPasswordChangeResult(StatusCodes.Status400BadRequest, code);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && code == "SESSION_REQUIRED")
            {
                return new StudentPasswordChangeResult(StatusCodes.Status401Unauthorized, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && code is "PASSWORD_CHANGE_REJECTED" or "IDEMPOTENCY_CONFLICT")
            {
                return new StudentPasswordChangeResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StudentPasswordChangeResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentPasswordChangeResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentPasswordChangeResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentPasswordChangeResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentPasswordChangeResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
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

    private sealed record IdentityPasswordChangeRequest(
        Guid SessionId,
        string? CurrentPassword,
        string? NewPassword);
}
