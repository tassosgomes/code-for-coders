namespace CodeForCoders.Identity.Api.Security;

public sealed record VerifiedServiceAssertion(Guid TenantId, Guid AssertionId, DateTimeOffset ExpiresOn, string Issuer = "");

public sealed record ServiceAssertionVerification(
    VerifiedServiceAssertion? Assertion,
    bool ScopeGranted);
