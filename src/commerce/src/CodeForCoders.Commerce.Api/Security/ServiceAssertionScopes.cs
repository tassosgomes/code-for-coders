namespace CodeForCoders.Commerce.Api.Security;

public static class ServiceAssertionScopes
{
    public const string ShowcaseRead = "showcase:read";
    public const string PurchaseIntentWrite = "purchase-intent:write";
    public const string AccessDecisionRead = "access-decision:read";

    public static readonly string[] Student = [ShowcaseRead, PurchaseIntentWrite];
    public static readonly string[] All = [ShowcaseRead, PurchaseIntentWrite, AccessDecisionRead];
}
