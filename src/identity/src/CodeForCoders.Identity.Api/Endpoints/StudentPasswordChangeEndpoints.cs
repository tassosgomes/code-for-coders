using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentPasswordChangeEndpoints
{
    private const string ChangeScope = "student-password-changes:execute";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentPasswordChangeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/password-changes", ChangeStudentPasswordAsync)
            .WithName("ChangeStudentPasswordInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentPasswordChangeV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ChangeStudentPasswordAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IChangeStudentPassword useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentPasswordChangeV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordChangeV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request is null
            || request.SessionId == Guid.Empty
            || string.IsNullOrEmpty(request.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "SessionId, current password, new password, and Idempotency-Key are required.");
        }

        try
        {
            await useCase.ExecuteAsync(
                new ChangeStudentPasswordInput(
                    assertion.TenantId,
                    request.SessionId,
                    request.CurrentPassword,
                    request.NewPassword,
                    idempotencyKey),
                cancellationToken);
            return Results.NoContent();
        }
        catch (StudentSessionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
        catch (StudentPasswordChangeException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
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
            ChangeScope,
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
