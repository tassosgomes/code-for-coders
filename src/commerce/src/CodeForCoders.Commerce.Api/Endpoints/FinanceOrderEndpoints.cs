using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.Sales.ListFinanceOrders;
using CodeForCoders.Commerce.Application.UseCases.Sales.GetFinanceOrder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
namespace CodeForCoders.Commerce.Api.Endpoints;

public static class FinanceOrderEndpoints
{
    public static void MapFinanceOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1/finance/orders").RequireAuthorization(FinanceAreaAuthorization.PolicyName).WithTags("FinanceOrders");
        group.MapGet("/", ListAsync).WithName("listFinanceOrdersInternal");
        group.MapGet("/{orderId:guid}", GetAsync).WithName("getFinanceOrderInternal");
    }
    private static async Task<IResult> ListAsync([AsParameters] FinanceOrderRequest request, HttpContext context,
        ITenantContext tenant, IListFinanceOrders useCase, CancellationToken cancellationToken)
    {
        if (!SetSchool(context, tenant)) return Results.Problem(statusCode: 401, extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(request.Status, request.CourseId, request.StudentId,
            request.CreatedFrom, request.CreatedTo, request.Page ?? 1, request.Size ?? 10), cancellationToken));
    }
    private static async Task<IResult> GetAsync(Guid orderId, HttpContext context, ITenantContext tenant,
        IGetFinanceOrder useCase, CancellationToken cancellationToken)
    {
        if (!SetSchool(context, tenant)) return Results.Problem(statusCode: 401, extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(orderId), cancellationToken));
    }
    private static bool SetSchool(HttpContext context, ITenantContext tenant)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var school) || school == Guid.Empty) return false;
        tenant.Set(school); return true;
    }
}
