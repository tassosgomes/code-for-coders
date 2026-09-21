namespace CodeForCoders.Commerce.Infra.Data.Configuration;

public static class CommerceModules
{
    public const string Catalog = "Catalog";
    public const string Sales = "Sales";
    public const string Entitlement = "Entitlement";

    // BA04 keeps Entitlement modular now and makes it the first extraction candidate later.
    public const bool EntitlementIsExtractionCandidate = true;
    public const string EntitlementExtractionCandidateReason = "First candidate for extraction (BA04).";

    public static IReadOnlyList<string> All { get; } = [Catalog, Sales, Entitlement];
}
