namespace CodeForCoders.Identity.Api.Security;

public sealed class StudentSessionTokenOptions
{
    public const string SectionName = "StudentSessionTokens";

    public string Issuer { get; set; } = "identity";

    public string SigningKeyId { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 5;

    public Dictionary<string, string> AudienceScopes { get; set; } = [];
}
