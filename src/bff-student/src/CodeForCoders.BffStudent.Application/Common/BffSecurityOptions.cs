using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.BffStudent.Application.Common;

public sealed class BffSecurityOptions
{
    public const string SectionName = "BffSecurity";

    [Required]
    public string SessionCookieName { get; set; } = "student_session";

    [Required]
    public string CsrfCookieName { get; set; } = "bff-student-csrf";

    [Required]
    public string CsrfHeaderName { get; set; } = "X-CSRF-TOKEN";

    [Required]
    public string SessionKeyPrefix { get; set; } = "bff-student:session:";

    [Range(1, 1440)]
    public int SessionTtlMinutes { get; set; } = 60;

    public Dictionary<string, string> RouteAudiences { get; set; } = new() { ["/api/v1/orders"] = "commerce", ["/api/v1/offers"] = "commerce", ["/api/v1/lessons"] = "learning", ["/api/v1/courses"] = "learning", ["/api/v1/my-courses"] = "learning", ["/api/v1/playback-sessions"] = "media" };

    public string ProxyAudience { get; set; } = string.Empty;

    public bool SessionCookieHttpOnly { get; set; } = true;

    public bool SessionCookieSecure { get; set; } = true;

    [Required]
    public string SessionCookieSameSite { get; set; } = "Lax";

    public bool UseOpaqueSessions { get; set; } = true;

    public bool BrowserReceivesAccessToken { get; set; }

    [Required]
    public string ReverseProxy { get; set; } = "YARP";
}
