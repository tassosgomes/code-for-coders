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
    public async Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/student-accounts")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertionTokenFactory.Create());
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
