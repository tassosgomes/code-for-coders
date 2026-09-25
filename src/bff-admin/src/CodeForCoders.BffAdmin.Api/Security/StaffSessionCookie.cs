using CodeForCoders.BffAdmin.Application.Common;

namespace CodeForCoders.BffAdmin.Api.Security;

public static class StaffSessionCookie
{
    public static void Append(
        HttpContext context,
        BffSecurityOptions settings,
        string value,
        DateTimeOffset expiresAt)
        => context.Response.Cookies.Append(settings.SessionCookieName, value, CreateOptions(settings, expiresAt));

    public static void Delete(HttpContext context, BffSecurityOptions settings)
    {
        var options = CreateOptions(settings, DateTimeOffset.UnixEpoch);
        options.MaxAge = TimeSpan.Zero;
        context.Response.Cookies.Append(settings.SessionCookieName, string.Empty, options);
    }

    private static CookieOptions CreateOptions(BffSecurityOptions settings, DateTimeOffset expiresAt)
    {
        var sameSite = Enum.TryParse<SameSiteMode>(settings.SessionCookieSameSite, true, out var parsedSameSite)
            ? parsedSameSite
            : SameSiteMode.Lax;
        return new CookieOptions
        {
            HttpOnly = settings.SessionCookieHttpOnly,
            Secure = settings.SessionCookieSecure,
            SameSite = sameSite,
            Path = "/",
            IsEssential = true,
            Expires = expiresAt,
        };
    }
}
