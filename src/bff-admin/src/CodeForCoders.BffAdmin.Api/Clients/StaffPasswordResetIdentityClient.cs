using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed class StaffPasswordResetIdentityClient(
    HttpClient httpClient,
    ServiceAssertionTokenFactory assertionTokenFactory) : IStaffPasswordResetIdentityClient
{
    private const string ResetScope = "staff-passwords:reset";

    public Task<StaffPasswordResetIdentityResult> ResetPasswordAsync(
        StaffPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => SendAsync(request, idempotencyKey, cancellationToken);

    private async Task<StaffPasswordResetIdentityResult> SendAsync(
        StaffPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "internal/v1/staff-password-resets")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            assertionTokenFactory.Create(ResetScope));
        message.Headers.Add("Idempotency-Key", idempotencyKey);

        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return new StaffPasswordResetIdentityResult(StatusCodes.Status204NoContent, null);
            }

            var code = await ReadCodeAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest && code == "INVALID_REQUEST")
            {
                return new StaffPasswordResetIdentityResult(StatusCodes.Status400BadRequest, code);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && code is not null
                && BusinessCodes.Contains(code, StringComparer.Ordinal))
            {
                return new StaffPasswordResetIdentityResult(StatusCodes.Status422UnprocessableEntity, code);
            }

            return new StaffPasswordResetIdentityResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (HttpRequestException)
        {
            return new StaffPasswordResetIdentityResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (TimeoutRejectedException)
        {
            return new StaffPasswordResetIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
        }
        catch (ExecutionRejectedException)
        {
            return new StaffPasswordResetIdentityResult(StatusCodes.Status502BadGateway, "IDENTITY_UNAVAILABLE");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StaffPasswordResetIdentityResult(StatusCodes.Status504GatewayTimeout, "IDENTITY_UNAVAILABLE");
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

    private static readonly string[] BusinessCodes =
    [
        "RESET_TOKEN_INVALID",
        "PASSWORD_POLICY_VIOLATION",
        "IDEMPOTENCY_CONFLICT",
    ];
}
