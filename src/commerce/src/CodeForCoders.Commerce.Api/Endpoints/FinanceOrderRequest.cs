using Microsoft.AspNetCore.Mvc;
namespace CodeForCoders.Commerce.Api.Endpoints;

public sealed record FinanceOrderRequest(string? Status, Guid? CourseId, Guid? StudentId,
    DateOnly? CreatedFrom, DateOnly? CreatedTo, [property: FromQuery(Name = "_page")] int? Page,
    [property: FromQuery(Name = "_size")] int? Size);
