namespace CodeForCoders.BffAdmin.Api.Clients;

public sealed record FinanceOrderFilters(string? Status, Guid? CourseId, Guid? StudentId,
    DateOnly? CreatedFrom, DateOnly? CreatedTo, int Page, int Size);
