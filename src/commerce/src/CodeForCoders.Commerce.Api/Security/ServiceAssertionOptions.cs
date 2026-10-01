namespace CodeForCoders.Commerce.Api.Security;

public sealed class ServiceAssertionOptions
{
    public const string SectionName = "ServiceAssertions";

    public string Audience { get; set; } = "commerce";

    /// <summary>Trusted issuers by name; each one carries its own public keys, scopes and tenants.</summary>
    public Dictionary<string, ServiceAssertionIssuerOptions> Issuers { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ServiceAssertionIssuerOptions
{
    /// <summary>Base64 SubjectPublicKeyInfo by <c>kid</c>; several entries allow key rotation with overlap.</summary>
    public Dictionary<string, string> PublicKeys { get; set; } = new(StringComparer.Ordinal);

    public string[] AllowedScopes { get; set; } = [];

    public string[] AllowedTenantIds { get; set; } = [];
}
