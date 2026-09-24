namespace CodeForCoders.BffStudent.Api.Security;

public sealed class StudentIdentityOptions
{
    public const string SectionName = "StudentIdentity";

    public string BaseAddress { get; set; } = string.Empty;

    public string Issuer { get; set; } = "bff-student";

    public string Audience { get; set; } = "identity-internal";

    public string Scope { get; set; } = "student-accounts:create student-accounts:confirm student-accounts:request-confirmation student-sessions:create student-sessions:validate student-sessions:revoke student-password-resets:request student-password-resets:execute";

    public string SigningKeyId { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;
}
