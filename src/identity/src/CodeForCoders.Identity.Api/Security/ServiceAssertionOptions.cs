namespace CodeForCoders.Identity.Api.Security;

public sealed class ServiceAssertionOptions
{
    public const string SectionName = "ServiceAssertions";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public Dictionary<string, string> PublicKeys { get; set; } = new(StringComparer.Ordinal);

    public string[] AllowedTenantIds { get; set; } = [];
}
