using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Security;

public sealed class BffSecurityMiddleware(
    RequestDelegate next,
    IOptions<BffSecurityOptions> securityOptions,
    IBffSessionStore sessionStore,
    IStudentSessionIdentityClient identityClient,
    IOptions<StudentSpaCorsOptions> corsOptions,
    TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = securityOptions.Value;
        var allowedOrigins = corsOptions.Value.AllowedOrigins;
        var isLogout = HttpMethods.IsDelete(context.Request.Method)
            && context.Request.Path.Equals("/api/v1/student-sessions/current", StringComparison.OrdinalIgnoreCase);
        var requiresSession = IsAuthenticatedRoute(context.Request.Path);
        if (!requiresSession && !isLogout)
        {
            await next(context);
            return;
        }

        var cookieValue = context.Request.Cookies[settings.SessionCookieName];
        var session = string.IsNullOrWhiteSpace(cookieValue)
            ? null
            : await sessionStore.GetAsync(cookieValue, context.RequestAborted);

        if (isLogout)
        {
            if (session is not null
                && (!HasAllowedOrigin(context, allowedOrigins)
                    || !CsrfProtection.IsValid(
                        context.Request.Headers[settings.CsrfHeaderName].FirstOrDefault(),
                        session.CsrfToken)))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "CSRF_INVALID",
                    "The CSRF proof is missing or invalid.");
                return;
            }

            if (session is not null)
            {
                BffSessionContext.Set(context, session);
            }

            await next(context);
            return;
        }

        if (session is null)
        {
            if (!string.IsNullOrWhiteSpace(cookieValue))
            {
                StudentSessionCookie.Delete(context, settings);
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current student session is required.");
            return;
        }

        if (IsUnsafeMethod(context.Request.Method)
            && !IsAnonymousPreLoginWrite(context.Request.Path)
            && (!HasAllowedOrigin(context, allowedOrigins)
                || !CsrfProtection.IsValid(
                    context.Request.Headers[settings.CsrfHeaderName].FirstOrDefault(),
                    session.CsrfToken)))
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
            session.StudentSessionId,
            string.IsNullOrWhiteSpace(audience) ? null : audience,
            context.RequestAborted);
        if (validation.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await sessionStore.RemoveAsync(cookieValue!, context.RequestAborted);
            StudentSessionCookie.Delete(context, settings);
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "SESSION_REQUIRED",
                "A current student session is required.");
            return;
        }

        if (validation.StatusCode != StatusCodes.Status200OK
            || validation.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(validation.Name)
            || (context.Request.Path.StartsWithSegments("/proxy")
                && string.IsNullOrWhiteSpace(validation.AccessToken))
            || validation.ExpiresAt <= timeProvider.GetUtcNow())
        {
            var statusCode = validation.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            await WriteProblemAsync(
                context,
                statusCode,
                "IDENTITY_UNAVAILABLE",
                "The student identity service is temporarily unavailable.");
            return;
        }

        var renewedSession = session with
        {
            AccountId = validation.AccountId,
            Name = validation.Name,
            ExpiresAt = validation.ExpiresAt,
        };
        await sessionStore.StoreAsync(cookieValue!, renewedSession, context.RequestAborted);
        StudentSessionCookie.Append(context, settings, cookieValue!, renewedSession.ExpiresAt);
        BffSessionContext.Set(context, renewedSession);
        BffSessionContext.SetAccessToken(context, validation.AccessToken);
        await next(context);
    }

    private static bool IsAuthenticatedRoute(PathString path)
    {
        if (path.StartsWithSegments("/proxy"))
        {
            return true;
        }

        if (!path.StartsWithSegments("/api/v1"))
        {
            return false;
        }

        return !IsAnonymousRoute(path);
    }

    private static bool IsAnonymousRoute(PathString path)
        => path.Equals("/api/v1/student-accounts", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/account-confirmations", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/account-confirmation-requests", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/student-sessions", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/password-reset-requests", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/password-resets", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnsafeMethod(string method)
        => HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

    private static bool IsAnonymousPreLoginWrite(PathString path)
        => IsAnonymousRoute(path);

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
