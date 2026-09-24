using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;
using CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentConfirmationEndpoints
{
    private const string ConfirmScope = "student-accounts:confirm";
    private const string RequestScope = "student-accounts:request-confirmation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentConfirmationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/account-confirmations", ConfirmStudentAccountAsync)
            .WithName("ConfirmStudentAccountInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentAccountConfirmationTokenV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/account-confirmation-requests", RequestStudentAccountConfirmationAsync)
            .WithName("RequestStudentAccountConfirmationInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentAccountConfirmationEmailV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ConfirmStudentAccountAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IConfirmStudentAccount useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, ConfirmScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentAccountConfirmationTokenV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentAccountConfirmationTokenV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Token) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Token and Idempotency-Key are required.");
        }

        await useCase.ExecuteAsync(
            new ConfirmStudentAccountInput(assertion.TenantId, request.Token, idempotencyKey),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RequestStudentAccountConfirmationAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRequestStudentAccountConfirmation useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, RequestScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentAccountConfirmationEmailV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentAccountConfirmationEmailV1>(
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
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email and Idempotency-Key are required.");
        }

        await useCase.ExecuteAsync(
            new RequestStudentAccountConfirmationInput(assertion.TenantId, request.Email, idempotencyKey),
            cancellationToken);
        return Results.Accepted();
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
