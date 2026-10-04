namespace CodeForCoders.Learning.Api.Security;

public sealed class AccessDecisionOptions
{
    public const string SectionName = "AccessDecision";
    public string BaseAddress { get; set; } = "http://localhost:5104/";
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyBase64 { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 2;
    public int CacheSeconds { get; set; } = 30;
}
