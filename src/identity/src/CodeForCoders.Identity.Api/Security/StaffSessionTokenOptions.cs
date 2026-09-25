namespace CodeForCoders.Identity.Api.Security;

public sealed class StaffSessionTokenOptions
{
    public const string SectionName = "StaffSessionTokens";

    public string Issuer { get; set; } = "identity";

    public string SigningKeyId { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;

    public Dictionary<string, string> PreviousSigningPublicKeys { get; set; } = [];

    public int LifetimeMinutes { get; set; } = 5;

    public Dictionary<string, string> AudienceScopes { get; set; } = [];
}
