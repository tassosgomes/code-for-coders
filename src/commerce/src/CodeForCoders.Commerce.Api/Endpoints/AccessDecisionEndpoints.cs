using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class AccessDecisionEndpoints
{
    public static void MapAccessDecisionEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("/internal/v1/access-decision")
            .RequireAuthorization(AccessDecisionPolicies.Read).WithTags("Access decisions")
            .MapGet("", DecideAsync).WithName("decideAccessInternal");

    private static async Task<IResult> DecideAsync([AsParameters] DecideAccessInput input, HttpContext context,
        IDecideAccess useCase, CancellationToken cancellationToken)
    {
        var decision = await useCase.ExecuteAsync(input, cancellationToken);
        context.Response.Headers.CacheControl = "private, max-age=30";
        return Results.Ok(decision);
    }
}
