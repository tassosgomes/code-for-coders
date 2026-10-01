using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.ResolveOfferReferences;
using System.Text.Json;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class OfferReferenceEndpoints
{
    public static void MapOfferReferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/offer-references/resolve", ResolveAsync)
            .RequireAuthorization(CatalogPolicies.ResolveReferences)
            .WithName("resolveOfferReferencesInternal").WithTags("AuditReferences");
    }

    private static async Task<IResult> ResolveAsync(JsonElement body, HttpContext context, ITenantContext tenantContext,
        IResolveOfferReferences useCase, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Results.Problem(statusCode: 401, title: "Access token tenant is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        tenantContext.Set(tenantId);
        return Results.Ok(await useCase.ExecuteAsync(new(body), cancellationToken));
    }
}
