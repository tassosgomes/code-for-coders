namespace CodeForCoders.Identity.Api.Security;

public sealed record VerifiedServiceAssertion(Guid TenantId, Guid AssertionId, DateTimeOffset ExpiresOn);
