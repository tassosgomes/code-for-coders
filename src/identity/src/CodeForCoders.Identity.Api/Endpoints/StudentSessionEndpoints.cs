using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStudentSession;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentSessionEndpoints
{
    private const string CreateScope = "student-sessions:create";
    private const string ValidateScope = "student-sessions:validate";
    private const string RevokeScope = "student-sessions:revoke";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/student-sessions", CreateStudentSessionAsync)
            .WithName("CreateStudentSessionInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentSessionLoginV1>("application/json")
            .Produces<StudentSessionCreatedV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/student-session-validations", ValidateStudentSessionAsync)
            .WithName("ValidateStudentSessionInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentSessionValidationV1>("application/json")
            .Produces<StudentSessionValidatedV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/internal/v1/student-session-revocations", RevokeStudentSessionAsync)
            .WithName("RevokeStudentSessionInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentSessionReferenceV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> CreateStudentSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IAuthenticateStudentSession useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, CreateScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StudentSessionLoginV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Email is null || request.Password is null
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email, password, and Idempotency-Key are required.");
        }

        try
        {
            var session = await useCase.ExecuteAsync(
                new AuthenticateStudentSessionInput(assertion.TenantId, request.Email, request.Password, idempotencyKey),
                cancellationToken);
            return Results.Ok(new StudentSessionCreatedV1(
                session.SessionId,
                session.AccountId,
                session.Name,
                session.ExpiresAt));
        }
        catch (StudentSessionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static async Task<IResult> ValidateStudentSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStudentSession useCase,
        StudentSessionTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, ValidateScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StudentSessionValidationV1>(httpContext, cancellationToken);
        if (request is null || request.SessionId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A sessionId is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Audience) && !tokenIssuer.CanIssueFor(request.Audience))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "AUDIENCE_NOT_ALLOWED", "The requested token audience is not authorized.");
        }

        var session = await useCase.ExecuteAsync(
            new ValidateStudentSessionInput(assertion.TenantId, request.SessionId),
            cancellationToken);
        if (session is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "The student session is missing or no longer active.");
        }

        var accessToken = string.IsNullOrWhiteSpace(request.Audience)
            ? null
            : tokenIssuer.Create(request.Audience, assertion.TenantId, session.AccountId, request.SessionId);
        return Results.Ok(new StudentSessionValidatedV1(
            session.AccountId,
            session.Name,
            session.ExpiresAt,
            accessToken));
    }

    private static async Task<IResult> RevokeStudentSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRevokeStudentSession useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, RevokeScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StudentSessionReferenceV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request is null || request.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "SessionId and Idempotency-Key are required.");
        }

        try
        {
            await useCase.ExecuteAsync(
                new RevokeStudentSessionInput(assertion.TenantId, request.SessionId, idempotencyKey),
                cancellationToken);
            return Results.NoContent();
        }
        catch (StudentSessionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static async Task<TRequest?> ReadRequestAsync<TRequest>(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<TRequest>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
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
