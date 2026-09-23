using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Security;

public sealed class BffSecurityMiddleware(
    RequestDelegate next,
    IOptions<BffSecurityOptions> securityOptions,
    IBffSessionStore sessionStore)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = securityOptions.Value;
        var sessionId = context.Request.Cookies[settings.SessionCookieName];
        OpaqueBffSession? session = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = await sessionStore.GetAsync(sessionId, context.RequestAborted);
            if (session is not null)
            {
                BffSessionContext.Set(context, session);
            }
        }

        if (context.Request.Path.StartsWithSegments("/proxy") && session is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (session is not null && IsUnsafeMethod(context.Request.Method)
            && !IsAnonymousPreLoginWrite(context.Request.Path)
            && !CsrfProtection.IsValid(
                context.Request.Cookies[settings.CsrfCookieName],
                context.Request.Headers[settings.CsrfHeaderName].FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private static bool IsUnsafeMethod(string method)
        => HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

    private static bool IsAnonymousPreLoginWrite(PathString path)
        => path.Equals("/api/v1/student-accounts", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/account-confirmations", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/account-confirmation-requests", StringComparison.OrdinalIgnoreCase);
}
