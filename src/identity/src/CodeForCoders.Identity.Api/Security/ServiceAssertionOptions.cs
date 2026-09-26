namespace CodeForCoders.Identity.Api.Security;

public sealed class ServiceAssertionOptions
{
    public const string SectionName = "ServiceAssertions";

    public string Audience { get; set; } = string.Empty;

    public Dictionary<string, ServiceAssertionIssuerOptions> Issuers { get; set; } = new(StringComparer.Ordinal);

    public string Issuer { get; set; } = string.Empty;

    public Dictionary<string, string> PublicKeys { get; set; } = new(StringComparer.Ordinal);

    public string[] AllowedTenantIds { get; set; } = [];

    public IReadOnlyDictionary<string, ServiceAssertionIssuerOptions> GetEffectiveIssuers()
    {
        var effective = new Dictionary<string, ServiceAssertionIssuerOptions>(Issuers, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(Issuer) && !effective.ContainsKey(Issuer))
        {
            effective[Issuer] = new ServiceAssertionIssuerOptions
            {
                PublicKeys = new Dictionary<string, string>(PublicKeys, StringComparer.Ordinal),
                AllowedScopes = [.. ServiceAssertionScopes.Student],
                AllowedTenantIds = [.. AllowedTenantIds],
            };
        }

        return effective;
    }
}

public sealed class ServiceAssertionIssuerOptions
{
    public Dictionary<string, string> PublicKeys { get; set; } = new(StringComparer.Ordinal);

    public string[] AllowedScopes { get; set; } = [];

    public string[] AllowedTenantIds { get; set; } = [];
}
