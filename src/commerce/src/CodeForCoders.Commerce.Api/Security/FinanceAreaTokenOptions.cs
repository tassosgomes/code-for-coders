namespace CodeForCoders.Commerce.Api.Security;

public sealed class FinanceAreaTokenOptions
{
    public const string SectionName = "FinanceAreaTokens";

    public string Issuer { get; set; } = "identity";

    public string Audience { get; set; } = "commerce";

    public string JwksUrl { get; set; } = "http://localhost:5101/internal/v1/jwks";

}
