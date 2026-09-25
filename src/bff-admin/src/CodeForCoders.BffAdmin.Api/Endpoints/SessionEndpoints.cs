using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class SessionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/staff-sessions", CreateStaffSessionAsync)
            .WithName("CreateStaffSession")
            .WithTags("StaffSession")
            .Accepts<StaffSessionLoginV1>("application/json")
            .Produces<StaffSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/api/v1/staff-sessions/current", (Delegate)GetCurrentStaffSessionAsync)
            .WithName("GetCurrentStaffSession")
            .WithTags("StaffSession")
            .Produces<StaffSessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapDelete("/api/v1/staff-sessions/current", EndCurrentStaffSessionAsync)
            .WithName("EndCurrentStaffSession")
            .WithTags("StaffSession")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapGet("/bff/session", (Delegate)GetStatusAsync)
            .WithName("GetBffAdminSessionStatus")
            .WithTags("Session")
            .Produces<BffSessionStatusResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CreateStaffSessionAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IBffSessionStore sessionStore,
        IOptions<BffSecurityOptions> securityOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync<StaffSessionLoginV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Email is null || request.Password is null
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email, password, and Idempotency-Key are required.");
        }

        var result = await identityClient.CreateSessionAsync(request, idempotencyKey, cancellationToken);
        var identitySession = result.Session;
        if (result.StatusCode != StatusCodes.Status200OK
            || identitySession is null
            || identitySession.SessionId == Guid.Empty
            || identitySession.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(identitySession.Name)
            || identitySession.Roles is null
            || identitySession.Permissions is null
            || identitySession.ExpiresAt <= timeProvider.GetUtcNow())
        {
            var (statusCode, code, title) = GetCreateProblem(result);
            return Problem(httpContext, statusCode, code, title);
        }

        var settings = securityOptions.Value;
        var cookieValue = CreateOpaqueValue();
        var csrfToken = CreateOpaqueValue();
        var session = new OpaqueBffSession(cookieValue, identitySession.SessionId, csrfToken, identitySession.ExpiresAt);
        await sessionStore.StoreAsync(session, cancellationToken);
        StaffSessionCookie.Append(httpContext, settings, cookieValue, session.ExpiresAt);
        return Results.Ok(new StaffSessionResponse(
            identitySession.AccountId,
            identitySession.Name,
            identitySession.Roles,
            identitySession.Permissions,
            csrfToken));
    }

    private static IResult GetCurrentStaffSessionAsync(HttpContext httpContext)
    {
        var session = BffSessionContext.Get(httpContext);
        var identitySession = BffSessionContext.GetValidatedSession(httpContext);
        return session is null || identitySession is null
            ? Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.")
            : Results.Ok(new StaffSessionResponse(
                identitySession.AccountId,
                identitySession.Name,
                identitySession.Roles,
                identitySession.Permissions,
                session.CsrfToken));
    }

    private static async Task<IResult> EndCurrentStaffSessionAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IBffSessionStore sessionStore,
        IOptions<BffSecurityOptions> securityOptions,
        CancellationToken cancellationToken)
    {
        var settings = securityOptions.Value;
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Idempotency-Key is required.");
        }

        var session = BffSessionContext.Get(httpContext);
        var cookieValue = httpContext.Request.Cookies[settings.SessionCookieName];
        if (session is null || string.IsNullOrWhiteSpace(cookieValue))
        {
            StaffSessionCookie.Delete(httpContext, settings);
            return Results.NoContent();
        }

        var result = await identityClient.RevokeSessionAsync(
            session.IdentitySessionId,
            idempotencyKey,
            cancellationToken);
        try
        {
            await sessionStore.RemoveAsync(cookieValue, cancellationToken);
        }
        finally
        {
            StaffSessionCookie.Delete(httpContext, settings);
        }

        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return Results.NoContent();
        }

        if (result.StatusCode == StatusCodes.Status422UnprocessableEntity && result.Code == "IDEMPOTENCY_CONFLICT")
        {
            return Problem(
                httpContext,
                StatusCodes.Status422UnprocessableEntity,
                "IDEMPOTENCY_KEY_REUSED",
                "The idempotency key was already used with a different request.");
        }

        var statusCode = result.StatusCode == StatusCodes.Status504GatewayTimeout
            ? StatusCodes.Status504GatewayTimeout
            : StatusCodes.Status502BadGateway;
        return Problem(
            httpContext,
            statusCode,
            "IDENTITY_UNAVAILABLE",
            "The staff identity service is temporarily unavailable.");
    }

    private static Task<Ok<BffSessionStatusResponse>> GetStatusAsync(HttpContext context)
        => Task.FromResult(TypedResults.Ok(new BffSessionStatusResponse(
            BffSessionContext.Get(context) is not null)));

    private static (int StatusCode, string Code, string Title) GetCreateProblem(StaffSessionIdentityCreatedResult result)
        => result.Code switch
        {
            "INVALID_REQUEST" => (StatusCodes.Status400BadRequest, "INVALID_REQUEST", "The login request is invalid."),
            "INVALID_CREDENTIALS" => (StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "E-mail ou senha inválidos."),
            "IDEMPOTENCY_CONFLICT" => (StatusCodes.Status422UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED", "The idempotency key was already used with a different request."),
            _ => (result.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway,
                "IDENTITY_UNAVAILABLE",
                "The staff identity service is temporarily unavailable."),
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
