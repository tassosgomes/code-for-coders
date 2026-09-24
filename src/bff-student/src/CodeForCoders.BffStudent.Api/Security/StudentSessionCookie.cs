using CodeForCoders.BffStudent.Application.Common;

namespace CodeForCoders.BffStudent.Api.Security;

public static class StudentSessionCookie
{
    public static void Append(
        HttpContext context,
        BffSecurityOptions settings,
        string opaqueValue,
        DateTimeOffset expiresAt)
        => context.Response.Cookies.Append(
            settings.SessionCookieName,
            opaqueValue,
            CreateOptions(settings, expiresAt));

    // Cookies.Delete descarta MaxAge; Append explícito garante Max-Age=0 além do Expires no passado.
    public static void Delete(HttpContext context, BffSecurityOptions settings)
    {
        var options = CreateOptions(settings, DateTimeOffset.UnixEpoch);
        options.MaxAge = TimeSpan.Zero;
        context.Response.Cookies.Append(settings.SessionCookieName, string.Empty, options);
    }

    private static CookieOptions CreateOptions(BffSecurityOptions settings, DateTimeOffset expiresAt)
        => new()
        {
            Expires = expiresAt,
            HttpOnly = settings.SessionCookieHttpOnly,
            IsEssential = true,
            Path = "/",
            SameSite = Enum.TryParse<SameSiteMode>(settings.SessionCookieSameSite, true, out var sameSite)
                ? sameSite
                : SameSiteMode.Lax,
            Secure = settings.SessionCookieSecure,
        };
}
