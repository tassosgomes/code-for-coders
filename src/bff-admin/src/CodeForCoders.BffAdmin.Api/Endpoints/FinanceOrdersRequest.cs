using Microsoft.AspNetCore.Mvc;
namespace CodeForCoders.BffAdmin.Api.Endpoints;

public sealed record FinanceOrdersRequest(string? Status, Guid? CourseId, Guid? StudentId, DateOnly? CreatedFrom, DateOnly? CreatedTo,
    [property: FromQuery(Name = "_page")] int? Page, [property: FromQuery(Name = "_size")] int? Size);
