using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Security;

public sealed class BffSecurityMiddleware(
    RequestDelegate next,
    IOptions<BffSecurityOptions> securityOptions,
    IBffSessionStore sessionStore,
    IStaffSessionIdentityClient identityClient,
    TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = securityOptions.Value;
        var isLogout = HttpMethods.IsDelete(context.Request.Method)
            && context.Request.Path.Equals("/api/v1/staff-sessions/current", StringComparison.OrdinalIgnoreCase);
        var requiresSession = IsProtectedRequest(context.Request);
        if (!requiresSession && !isLogout)
        {
            if (RequiresAllowedOrigin(context.Request) && !HasAllowedOrigin(context, settings.AllowedOrigins))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "CSRF_INVALID",
                    "The request origin is missing or invalid.");
                return;
            }

            await next(context);
            return;
        }

        var cookieValue = context.Request.Cookies[settings.SessionCookieName];
        var session = string.IsNullOrWhiteSpace(cookieValue)
            ? null
            : await sessionStore.GetAsync(cookieValue, context.RequestAborted);
        if (session is not null && !string.Equals(session.SessionId, cookieValue, StringComparison.Ordinal))
        {
            session = null;
        }

        if (isLogout)
        {
            if (session is null)
            {
                if (!string.IsNullOrWhiteSpace(cookieValue))
                {
                    StaffSessionCookie.Delete(context, settings);
                }

                await next(context);
                return;
            }

            if (!HasAllowedOrigin(context, settings.AllowedOrigins)
                || !CsrfProtection.IsValid(
                    session.CsrfToken,
                    context.Request.Headers[settings.CsrfHeaderName].FirstOrDefault()))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "CSRF_INVALID",
                    "The CSRF proof is missing or invalid.");
                return;
            }

            BffSessionContext.Set(context, session);
            await next(context);
            return;
        }

        if (session is null || string.IsNullOrWhiteSpace(cookieValue))
        {
            if (!string.IsNullOrWhiteSpace(cookieValue))
            {
                StaffSessionCookie.Delete(context, settings);
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current staff session is required.");
            return;
        }

        if (IsUnsafeMethod(context.Request.Method)
            && (!HasAllowedOrigin(context, settings.AllowedOrigins)
                || !CsrfProtection.IsValid(
                    session.CsrfToken,
                    context.Request.Headers[settings.CsrfHeaderName].FirstOrDefault())))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "CSRF_INVALID",
                "The CSRF proof is missing or invalid.");
            return;
        }

        var audience = context.Request.Path.StartsWithSegments("/proxy")
            ? settings.ProxyAudience
            : null;
        var validation = await identityClient.ValidateSessionAsync(
            session.IdentitySessionId,
            string.IsNullOrWhiteSpace(audience) ? null : audience,
            context.RequestAborted);
        if (validation.StatusCode == StatusCodes.Status401Unauthorized
            && validation.Code == "SESSION_REQUIRED")
        {
            await sessionStore.RemoveAsync(cookieValue, context.RequestAborted);
            StaffSessionCookie.Delete(context, settings);
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current staff session is required.");
            return;
        }

        var identitySession = validation.Session;
        if (validation.StatusCode != StatusCodes.Status200OK
            || identitySession is null
            || identitySession.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(identitySession.Name)
            || identitySession.Roles is null
            || identitySession.Permissions is null
            || identitySession.ExpiresAt <= timeProvider.GetUtcNow())
        {
            var statusCode = validation.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            await WriteProblemAsync(
                context,
                statusCode,
                "IDENTITY_UNAVAILABLE",
                "The staff identity service is temporarily unavailable.");
            return;
        }

        var renewedSession = session with { ExpiresAt = identitySession.ExpiresAt };
        await sessionStore.StoreAsync(renewedSession, context.RequestAborted);
        StaffSessionCookie.Append(context, settings, cookieValue, renewedSession.ExpiresAt);
        BffSessionContext.Set(context, renewedSession);
        BffSessionContext.SetValidatedSession(context, identitySession);
        if (context.Request.Path.StartsWithSegments("/proxy")
            && string.IsNullOrWhiteSpace(identitySession.AccessToken))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "PERMISSION_DENIED",
                "The current session has no token for the requested service.");
            return;
        }

        await next(context);
    }

    private static bool IsProtectedRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/proxy"))
        {
            return true;
        }

        if (request.Path.Equals("/bff/session", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Path.StartsWithSegments("/api/v1") && !IsAnonymousRoute(request);
    }

    private static bool IsAnonymousRoute(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
            && (request.Path.Equals("/api/v1/staff-sessions", StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals("/api/v1/staff-password-resets", StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals("/api/v1/staff-password-reset-requests", StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals("/api/v1/staff-invitation-lookups", StringComparison.OrdinalIgnoreCase)
                || request.Path.Equals("/api/v1/staff-invitation-acceptances", StringComparison.OrdinalIgnoreCase));

    private static bool RequiresAllowedOrigin(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
            && request.Path.Equals("/api/v1/staff-invitation-acceptances", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsafeMethod(string method)
        => HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

    private static bool HasAllowedOrigin(HttpContext context, string[] allowedOrigins)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(origin)
            && allowedOrigins.Contains(origin, StringComparer.Ordinal);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string title)
        => Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            }).ExecuteAsync(context);
}
