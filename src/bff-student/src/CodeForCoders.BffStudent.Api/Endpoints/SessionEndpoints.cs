using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.ApiModels;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class SessionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/student-sessions", CreateStudentSessionAsync)
            .WithName("CreateStudentSession")
            .WithTags("Student")
            .Accepts<StudentSessionLoginV1>("application/json")
            .Produces<StudentSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapGet("/api/v1/student-sessions/current", GetCurrentStudentSessionAsync)
            .WithName("GetCurrentStudentSession")
            .WithTags("Student")
            .Produces<StudentSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapDelete("/api/v1/student-sessions/current", EndCurrentStudentSessionAsync)
            .WithName("EndCurrentStudentSession")
            .WithTags("Student")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> CreateStudentSessionAsync(
        HttpContext httpContext,
        IStudentSessionIdentityClient identityClient,
        IBffSessionStore sessionStore,
        IOptions<BffSecurityOptions> securityOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync<StudentSessionLoginV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Email is null || request.Password is null
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email, password, and Idempotency-Key are required.");
        }

        var result = await identityClient.CreateSessionAsync(request, idempotencyKey, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK
            || result.SessionId == Guid.Empty
            || result.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(result.Name)
            || result.ExpiresAt <= timeProvider.GetUtcNow())
        {
            var (statusCode, code, title) = GetCreateProblem(result);
            return Problem(httpContext, statusCode, code, title);
        }

        var cookieValue = CreateOpaqueValue();
        var csrfToken = CreateOpaqueValue();
        var session = new OpaqueBffSession(
            result.SessionId,
            result.AccountId,
            result.Name!,
            csrfToken,
            result.ExpiresAt);
        await sessionStore.StoreAsync(cookieValue, session, cancellationToken);
        StudentSessionCookie.Append(httpContext, securityOptions.Value, cookieValue, session.ExpiresAt);
        return Results.Ok(new StudentSessionResponse(session.AccountId, session.Name, session.CsrfToken));
    }

    private static IResult GetCurrentStudentSessionAsync(HttpContext httpContext)
    {
        var session = BffSessionContext.Get(httpContext);
        return session is null
            ? Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current student session is required.")
            : Results.Ok(new StudentSessionResponse(session.AccountId, session.Name, session.CsrfToken));
    }

    private static async Task<IResult> EndCurrentStudentSessionAsync(
        HttpContext httpContext,
        IStudentSessionIdentityClient identityClient,
        IBffSessionStore sessionStore,
        IOptions<BffSecurityOptions> securityOptions,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Idempotency-Key is required.");
        }

        var session = BffSessionContext.Get(httpContext);
        var cookieValue = httpContext.Request.Cookies[securityOptions.Value.SessionCookieName];
        if (session is null || string.IsNullOrWhiteSpace(cookieValue))
        {
            StudentSessionCookie.Delete(httpContext, securityOptions.Value);
            return Results.NoContent();
        }

        var result = await identityClient.RevokeSessionAsync(
            session.StudentSessionId,
            idempotencyKey,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status422UnprocessableEntity)
        {
            return Problem(
                httpContext,
                result.StatusCode,
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.");
        }

        try
        {
            await sessionStore.RemoveAsync(cookieValue, cancellationToken);
        }
        finally
        {
            StudentSessionCookie.Delete(httpContext, securityOptions.Value);
        }

        if (result.StatusCode != StatusCodes.Status204NoContent)
        {
            var statusCode = result.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            return Problem(
                httpContext,
                statusCode,
                "IDENTITY_UNAVAILABLE",
                "The student identity service is temporarily unavailable.");
        }

        return Results.NoContent();
    }

    private static (int StatusCode, string Code, string Title) GetCreateProblem(StudentSessionCreatedResult result)
        => result.Code switch
        {
            "INVALID_REQUEST" => (StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The login request is invalid."),
            "INVALID_CREDENTIALS" => (StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Email or password is incorrect."),
            "EMAIL_NOT_CONFIRMED" => (StatusCodes.Status422UnprocessableEntity, "EMAIL_NOT_CONFIRMED", "Confirm the account email before signing in."),
            "IDEMPOTENCY_CONFLICT" => (StatusCodes.Status422UnprocessableEntity, "IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different request."),
            _ => (result.StatusCode is StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway,
                "IDENTITY_UNAVAILABLE",
                "The student identity service is temporarily unavailable."),
        };

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

    private static string CreateOpaqueValue()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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
