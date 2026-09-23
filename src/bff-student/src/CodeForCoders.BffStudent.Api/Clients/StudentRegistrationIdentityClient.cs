using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class StudentRegistrationIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStudentRegistrationIdentityClient
{
    private const string RegistrationScope = "student-accounts:create";
    private const string ConfirmationScope = "student-accounts:confirm";
    private const string ConfirmationRequestScope = "student-accounts:request-confirmation";

    public async Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/student-accounts")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create(RegistrationScope));
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return new StudentRegistrationResult(StatusCodes.Status202Accepted, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StudentRegistrationResult(StatusCodes.Status400BadRequest, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && code is "ACCOUNT_ALREADY_EXISTS" or "PASSWORD_POLICY_VIOLATION" or "IDEMPOTENCY_CONFLICT")
            {
                return new StudentRegistrationResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StudentRegistrationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentRegistrationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentRegistrationResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentRegistrationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentRegistrationResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
    }

    public Task<StudentConfirmationResult> ConfirmAsync(
        StudentAccountConfirmationTokenV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendConfirmationAsync(
            "internal/v1/account-confirmations",
            request,
            idempotencyKey,
            ConfirmationScope,
            HttpStatusCode.NoContent,
            cancellationToken);

    public Task<StudentConfirmationResult> RequestConfirmationAsync(
        StudentAccountConfirmationEmailV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendConfirmationAsync(
            "internal/v1/account-confirmation-requests",
            request,
            idempotencyKey,
            ConfirmationRequestScope,
            HttpStatusCode.Accepted,
            cancellationToken);

    private async Task<StudentConfirmationResult> SendConfirmationAsync<TRequest>(
        string path,
        TRequest request,
        string idempotencyKey,
        string requiredScope,
        HttpStatusCode successStatus,
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
                return new StudentConfirmationResult((int)successStatus, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StudentConfirmationResult(StatusCodes.Status400BadRequest, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && code is "INVALID_VERIFICATION_TOKEN" or "IDEMPOTENCY_CONFLICT")
            {
                return new StudentConfirmationResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StudentConfirmationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StudentConfirmationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StudentConfirmationResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StudentConfirmationResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StudentConfirmationResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
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
