namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class EntitlementRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
