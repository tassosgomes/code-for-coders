namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record OrderScope(Guid TenantId, Guid StudentId, string KeyHash);
