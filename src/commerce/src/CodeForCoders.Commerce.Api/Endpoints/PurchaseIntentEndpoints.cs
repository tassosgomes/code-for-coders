using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;
using Microsoft.AspNetCore.Mvc;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class PurchaseIntentEndpoints
{
    public static void MapPurchaseIntentEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/internal/v1/showcase/offers/{offerId:guid}/purchase-intents", RegisterAsync)
            .RequireAuthorization(ShowcasePolicies.PurchaseIntent).WithName("registerPurchaseIntentInternal");

    private static async Task<IResult> RegisterAsync(Guid offerId, HttpContext context,
        IRegisterPurchaseIntent useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var key = context.Request.Headers["Idempotency-Key"];
        var result = await useCase.ExecuteAsync(new(offerId, key.Count == 1 ? key[0]! : string.Empty), cancellationToken);
        return Results.Json(result, statusCode: StatusCodes.Status202Accepted);
    }
}
