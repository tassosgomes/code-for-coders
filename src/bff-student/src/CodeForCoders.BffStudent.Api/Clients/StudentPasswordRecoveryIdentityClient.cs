using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class StudentPasswordRecoveryIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStudentPasswordRecoveryIdentityClient
{
    private const string RequestScope = "student-password-resets:request";
    private const string ResetScope = "student-password-resets:execute";

    public Task<StudentPasswordRecoveryResult> RequestPasswordResetAsync(
        StudentPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            "internal/v1/password-reset-requests",
            request,
            idempotencyKey,
            RequestScope,
            HttpStatusCode.Accepted,
            ["IDEMPOTENCY_CONFLICT"],
            cancellationToken);

    public Task<StudentPasswordRecoveryResult> ResetPasswordAsync(
        StudentPasswordResetV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(
            "internal/v1/password-resets",
            request,
            idempotencyKey,
            ResetScope,
            HttpStatusCode.NoContent,
            ["PASSWORD_RESET_REJECTED", "IDEMPOTENCY_CONFLICT"],
            cancellationToken);

    private async Task<StudentPasswordRecoveryResult> SendAsync<TRequest>(
        string path,
        TRequest request,
        string idempotencyKey,
        string requiredScope,
        HttpStatusCode successStatus,
        string[] businessCodes,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(requiredScope));
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == successStatus)
            {
                return new StudentPasswordRecoveryResult((int)successStatus, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StudentPasswordRecoveryResult(StatusCodes.Status400BadRequest, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code is not null
                && businessCodes.Contains(code, StringComparer.Ordinal))
            {
                return new StudentPasswordRecoveryResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StudentPasswordRecoveryResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentPasswordRecoveryResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentPasswordRecoveryResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentPasswordRecoveryResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentPasswordRecoveryResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
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
}
