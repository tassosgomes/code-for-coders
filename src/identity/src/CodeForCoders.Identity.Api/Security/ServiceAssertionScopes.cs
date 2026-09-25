namespace CodeForCoders.Identity.Api.Security;

public static class ServiceAssertionScopes
{
    public static readonly string[] Student =
    [
        "student-accounts:create",
        "student-accounts:confirm",
        "student-accounts:request-confirmation",
        "student-sessions:create",
        "student-sessions:validate",
        "student-sessions:revoke",
        "student-password-resets:request",
        "student-password-resets:execute",
        "student-password-changes:execute",
    ];

    public static readonly string[] Staff =
    [
        "staff-sessions:create",
        "staff-sessions:validate",
        "staff-sessions:revoke",
        "staff-passwords:reset",
        "staff-invitations:read",
        "staff-invitations:write",
        "staff-members:read",
        "staff-members:write",
    ];
}
