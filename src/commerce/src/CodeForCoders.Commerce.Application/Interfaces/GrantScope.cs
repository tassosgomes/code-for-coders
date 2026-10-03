namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record GrantScope(Guid TenantId, Guid ActorId, string KeyHash);
