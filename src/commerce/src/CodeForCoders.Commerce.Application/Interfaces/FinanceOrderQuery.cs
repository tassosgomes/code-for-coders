namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record FinanceOrderQuery(string? Status, Guid? CourseId, Guid? StudentId,
    DateTimeOffset? CreatedFrom, DateTimeOffset? CreatedBefore, int Page, int Size);
