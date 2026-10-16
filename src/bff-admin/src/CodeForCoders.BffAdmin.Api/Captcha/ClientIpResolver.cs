using System.Net;

namespace CodeForCoders.BffAdmin.Api.Captcha;

/// <summary>
/// Resolves the client IP for the siteverify and for the anonymous rate limit. <c>CF-Connecting-IP</c> is only
/// trustworthy when the traffic goes through Cloudflare (ADR-0023); otherwise a client could forge it, so the value
/// is used only as the siteverify <c>remoteip</c> and as a rate-limit key, never for authorization.
/// </summary>
public static class ClientIpResolver
{
    public const string CloudflareHeader = "CF-Connecting-IP";

    public static string? Resolve(HttpContext context)
    {
        var header = context.Request.Headers[CloudflareHeader].ToString().Trim();
        if (header.Length > 0 && IPAddress.TryParse(header, out var forwarded))
        {
            return Normalize(forwarded);
        }

        var remote = context.Connection.RemoteIpAddress;
        return remote is null ? null : Normalize(remote);
    }

    private static string Normalize(IPAddress address)
        => (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}
