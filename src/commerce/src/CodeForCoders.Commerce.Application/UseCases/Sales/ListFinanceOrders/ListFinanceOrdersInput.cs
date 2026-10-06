namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListFinanceOrders;

public sealed record ListFinanceOrdersInput(string? Status, Guid? CourseId, Guid? StudentId,
    DateOnly? CreatedFrom, DateOnly? CreatedTo, int Page, int Size);
