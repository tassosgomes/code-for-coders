namespace CodeForCoders.Media.Api.Security;

public sealed class MediaTokenOptions
{
    public const string SectionName = "MediaTokens";

    public string Issuer { get; set; } = "identity";

    public string Audience { get; set; } = "media";

    public string JwksUrl { get; set; } = string.Empty;
}
