using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffAdmin.Api.Security;

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

        if (session is not null && IsUnsafeMethod(context.Request.Method) && !IsStaffPasswordResetRequest(context.Request)
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

    private static bool IsStaffPasswordResetRequest(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
            && string.Equals(request.Path.Value, "/api/v1/staff-password-resets", StringComparison.Ordinal);
}
