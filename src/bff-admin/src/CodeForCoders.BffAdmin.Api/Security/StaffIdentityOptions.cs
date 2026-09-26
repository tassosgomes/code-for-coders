namespace CodeForCoders.BffAdmin.Api.Security;

public sealed class StaffIdentityOptions
{
    public const string SectionName = "StaffIdentity";

    public string BaseAddress { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKeyId { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;
}
