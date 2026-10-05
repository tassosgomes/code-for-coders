namespace CodeForCoders.Commerce.Application.UseCases.Sales.CreateOrder;

public sealed record CreateOrderInput(Guid TenantId, Guid StudentId, Guid OfferId, string IdempotencyKey, string? TraceParent);
