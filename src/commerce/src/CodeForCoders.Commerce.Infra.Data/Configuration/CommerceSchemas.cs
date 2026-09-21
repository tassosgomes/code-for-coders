namespace CodeForCoders.Commerce.Infra.Data.Configuration;

public static class CommerceSchemas
{
    public const string Catalog = "catalog";
    public const string Sales = "sales";
    public const string Entitlement = "entitlement";

    public static IReadOnlyList<string> All { get; } = [Catalog, Sales, Entitlement];
}
