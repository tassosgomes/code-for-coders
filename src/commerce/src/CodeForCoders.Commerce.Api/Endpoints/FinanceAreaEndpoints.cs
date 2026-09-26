using CodeForCoders.Commerce.Api.ApiModels;
using CodeForCoders.Commerce.Api.Security;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class FinanceAreaEndpoints
{
    public static void MapFinanceAreaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/finance-area", () => Results.Ok(new FinanceAreaResponse("reserved")))
            .RequireAuthorization(FinanceAreaAuthorization.PolicyName)
            .WithName("GetFinanceAreaInternal")
            .WithTags("FinanceArea")
            .Produces<FinanceAreaResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
