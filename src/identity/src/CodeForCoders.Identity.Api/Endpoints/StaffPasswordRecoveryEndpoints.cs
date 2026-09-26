using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStaffPasswordReset;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StaffPasswordRecoveryEndpoints
{
    private const string RequestScope = "staff-passwords:reset";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffPasswordRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/staff-password-reset-requests", RequestStaffPasswordResetAsync)
            .WithName("RequestStaffPasswordResetInternal")
            .WithTags("StaffIdentity")
            .Accepts<StaffPasswordRecoveryRequestV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> RequestStaffPasswordResetAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRequestStaffPasswordReset useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StaffPasswordRecoveryRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffPasswordRecoveryRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var email = request?.Email;
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(email)
            || email.Length > 254
            || !new EmailAddressAttribute().IsValid(email)
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email and Idempotency-Key must be valid.");
        }

        var result = await useCase.ExecuteAsync(
            new RequestStaffPasswordResetInput(assertion.TenantId, email, idempotencyKey),
            cancellationToken);
        return Results.StatusCode(result.StatusCode);
    }

    private static async Task<VerifiedServiceAssertion?> VerifyServiceAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.FirstOrDefault(),
                out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await assertionVerifier.VerifyAsync(
            authorization.Parameter,
            RequestScope,
            cancellationToken);
    }

    private static IResult Problem(HttpContext httpContext, int statusCode, string code, string title)
        => Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            });
}
