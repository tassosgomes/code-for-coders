namespace CodeForCoders.Learning.Api.Security;

public sealed class LearningTokenOptions
{
    public const string SectionName = "LearningTokens";

    public string Issuer { get; set; } = "identity";

    public string Audience { get; set; } = "learning";

    public string JwksUrl { get; set; } = string.Empty;
}
