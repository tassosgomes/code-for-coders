namespace CodeForCoders.Commerce.Application.UseCases.Sales.CancelOrder;

public sealed record CancelOrderInput(Guid OrderId, Guid StudentId, string? TraceParent = null);
