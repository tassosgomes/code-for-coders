namespace CodeForCoders.Commerce.Api.Security;

public sealed record VerifiedServiceAssertion(
    string Issuer,
    Guid TenantId,
    Guid AssertionId,
    DateTimeOffset ExpiresOn,
    IReadOnlyList<string> GrantedScopes);
