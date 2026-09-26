using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StaffSessionEndpoints
{
    private const string CreateScope = "staff-sessions:create";
    private const string ValidateScope = "staff-sessions:validate";
    private const string RevokeScope = "staff-sessions:revoke";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/staff-sessions", CreateStaffSessionAsync)
            .WithName("CreateStaffSessionInternal")
            .WithTags("StaffIdentity")
            .Accepts<StaffSessionLoginV1>("application/json")
            .Produces<StaffSessionCreatedV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/staff-session-validations", ValidateStaffSessionAsync)
            .WithName("ValidateStaffSessionInternal")
            .WithTags("StaffIdentity")
            .Accepts<StaffSessionValidationV1>("application/json")
            .Produces<StaffSessionValidatedV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/internal/v1/staff-session-revocations", RevokeStaffSessionAsync)
            .WithName("RevokeStaffSessionInternal")
            .WithTags("StaffIdentity")
            .Accepts<StaffSessionReferenceV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> CreateStaffSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IAuthenticateStaffSession useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, CreateScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StaffSessionLoginV1>(httpContext, cancellationToken);
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
                new AuthenticateStaffSessionInput(assertion.TenantId, request.Email, request.Password, idempotencyKey),
                cancellationToken);
            return Results.Ok(new StaffSessionCreatedV1(
                session.SessionId,
                session.AccountId,
                session.Name,
                session.Roles,
                session.Permissions,
                session.ExpiresAt));
        }
        catch (StaffSessionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static async Task<IResult> ValidateStaffSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession useCase,
        StaffSessionTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, ValidateScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StaffSessionValidationV1>(httpContext, cancellationToken);
        if (request is null || request.SessionId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A session identifier is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Audience) && !tokenIssuer.CanIssueFor(request.Audience))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "AUDIENCE_NOT_ALLOWED", "The requested token audience is not allowed.");
        }

        var validation = await useCase.ExecuteAsync(
            new ValidateStaffSessionInput(assertion.TenantId, request.SessionId),
            cancellationToken);
        if (validation is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        var session = validation.Session;
        var accessToken = string.IsNullOrWhiteSpace(request.Audience)
            ? null
            : tokenIssuer.Create(
                request.Audience,
                assertion.TenantId,
                session.AccountId,
                session.SessionId,
                session.Roles,
                session.Permissions);
        return Results.Ok(new StaffSessionValidatedV1(
            session.AccountId,
            session.Name,
            session.Roles,
            session.Permissions,
            session.ExpiresAt,
            accessToken));
    }

    private static async Task<IResult> RevokeStaffSessionAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRevokeStaffSession useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, RevokeScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var request = await ReadRequestAsync<StaffSessionReferenceV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request is null || request.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "SessionId and Idempotency-Key are required.");
        }

        try
        {
            await useCase.ExecuteAsync(
                new RevokeStaffSessionInput(assertion.TenantId, request.SessionId, idempotencyKey),
                cancellationToken);
            return Results.NoContent();
        }
        catch (StaffSessionException exception)
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
