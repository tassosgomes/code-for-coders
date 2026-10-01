namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PurchaseIntentRegistration(Guid TenantId, Guid OfferId, string KeyHash, DateTimeOffset Now);
