namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListStudentOrders;

public sealed record ListStudentOrdersInput(Guid StudentId, int Page, int Size);
