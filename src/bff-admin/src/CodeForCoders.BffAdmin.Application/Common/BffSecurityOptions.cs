using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.BffAdmin.Application.Common;

public sealed class BffSecurityOptions
{
    public const string SectionName = "BffSecurity";

    [Required]
    public string SessionCookieName { get; set; } = "__Host-bff-admin-session";

    [Required]
    public string CsrfCookieName { get; set; } = "bff-admin-csrf";

    [Required]
    public string CsrfHeaderName { get; set; } = "X-CSRF-TOKEN";

    [Required]
    public string SessionKeyPrefix { get; set; } = "bff-admin:session:";

    [Range(1, 1440)]
    public int SessionTtlMinutes { get; set; } = 60;

    public bool SessionCookieHttpOnly { get; set; } = true;

    public bool SessionCookieSecure { get; set; } = true;

    [Required]
    public string SessionCookieSameSite { get; set; } = "Lax";

    public bool UseOpaqueSessions { get; set; } = true;

    public bool BrowserReceivesAccessToken { get; set; }

    [Required]
    public string ReverseProxy { get; set; } = "YARP";
}
