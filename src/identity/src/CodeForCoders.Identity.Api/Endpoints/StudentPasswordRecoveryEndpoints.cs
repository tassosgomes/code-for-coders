using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentPasswordRecoveryEndpoints
{
    private const string RequestScope = "student-password-resets:request";
    private const string ResetScope = "student-password-resets:execute";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentPasswordRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/password-reset-requests", RequestStudentPasswordResetAsync)
            .WithName("RequestStudentPasswordResetInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentPasswordResetEmailV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/password-resets", ResetStudentPasswordAsync)
            .WithName("ResetStudentPasswordInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentPasswordResetInputV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> RequestStudentPasswordResetAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRequestStudentPasswordReset useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, RequestScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentPasswordResetEmailV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordResetEmailV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Email)
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email and Idempotency-Key are required.");
        }

        await useCase.ExecuteAsync(
            new RequestStudentPasswordResetInput(assertion.TenantId, request.Email, idempotencyKey),
            cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ResetStudentPasswordAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IResetStudentPassword useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, ResetScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentPasswordResetInputV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordResetInputV1>(
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
            new ResetStudentPasswordInput(assertion.TenantId, request.Token, request.NewPassword, idempotencyKey),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<VerifiedServiceAssertion?> VerifyServiceAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.FirstOrDefault(),
                out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await assertionVerifier.VerifyAsync(authorization.Parameter, requiredScope, cancellationToken);
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
