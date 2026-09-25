using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StaffPasswordResetEndpoints
{
    private const string ResetScope = "staff-passwords:reset";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffPasswordResetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/staff-password-resets", ResetStaffPasswordAsync)
            .WithName("ResetStaffPasswordInternal")
            .WithTags("StaffIdentity")
            .Accepts<StaffPasswordResetInputV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ResetStaffPasswordAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IResetStaffPassword useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StaffPasswordResetInputV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffPasswordResetInputV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Token, new password, and Idempotency-Key are required.");
        }

        await useCase.ExecuteAsync(
            new ResetStaffPasswordInput(assertion.TenantId, request.Token, request.NewPassword, idempotencyKey),
            cancellationToken);
        return Results.NoContent();
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
            ResetScope,
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
