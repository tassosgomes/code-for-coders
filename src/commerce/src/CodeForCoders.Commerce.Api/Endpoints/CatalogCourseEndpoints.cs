using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CatalogCourses.ListCatalogCourses;
using CodeForCoders.Commerce.Application.UseCases.CatalogCourses.GetCatalogCourse;
using CodeForCoders.Commerce.Application.UseCases.CatalogCourses.UpdateCatalogCourse;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.CreateOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UpdateOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.DeleteOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.PublishOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;
using System.Text.Json;
using System.Security.Claims;

namespace CodeForCoders.Commerce.Api.Endpoints;

public static class CatalogCourseEndpoints
{
    public static void MapCatalogCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1/catalog").RequireAuthorization(CatalogPolicies.EditOffers).WithTags("Catalog");
        group.MapGet("/courses", ListAsync).WithName("listCatalogCoursesInternal");
        group.MapGet("/courses/{courseId:guid}", GetAsync).WithName("getCatalogCourseInternal");
        group.MapPatch("/courses/{courseId:guid}", UpdateAsync).WithName("updateCatalogCourseInternal");
        group.MapPost("/courses/{courseId:guid}/offers", CreateOfferAsync).WithName("createOfferInternal");
        group.MapPatch("/offers/{offerId:guid}", UpdateOfferAsync).WithName("updateOfferInternal");
        group.MapPost("/offers/{offerId:guid}/publish", PublishOfferAsync).WithName("publishOfferInternal");
        group.MapPost("/offers/{offerId:guid}/unpublish", UnpublishOfferAsync).WithName("unpublishOfferInternal");
        group.MapDelete("/offers/{offerId:guid}", DeleteOfferAsync).WithName("deleteOfferInternal");
    }

    private static async Task<IResult> CreateOfferAsync(Guid courseId, JsonElement body, HttpContext context,
        ITenantContext tenantContext, ICreateOffer useCase, CancellationToken cancellationToken)
    {
        if (!SetActor(context, tenantContext, out var actorId)) return InvalidToken();
        var output = await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, courseId,
            context.Request.Headers["Idempotency-Key"].ToString(), body), cancellationToken);
        return Results.Created($"/api/v1/catalog/offers/{output.OfferId:D}", output);
    }

    private static async Task<IResult> UpdateOfferAsync(Guid offerId, JsonElement body, HttpContext context,
        ITenantContext tenantContext, IUpdateOffer useCase, CancellationToken cancellationToken)
    {
        if (!SetActor(context, tenantContext, out var actorId)) return InvalidToken();
        return Results.Ok(await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, offerId,
            context.Request.Headers["Idempotency-Key"].ToString(), body, System.Diagnostics.Activity.Current?.Id), cancellationToken));
    }

    private static async Task<IResult> PublishOfferAsync(Guid offerId, HttpContext context,
        ITenantContext tenantContext, IPublishOffer useCase, CancellationToken cancellationToken)
    {
        if (!SetActor(context, tenantContext, out var actorId)) return InvalidToken();
        return Results.Ok(await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, offerId,
            context.Request.Headers["Idempotency-Key"].ToString(), System.Diagnostics.Activity.Current?.Id), cancellationToken));
    }

    private static async Task<IResult> UnpublishOfferAsync(Guid offerId, HttpContext context,
        ITenantContext tenantContext, IUnpublishOffer useCase, CancellationToken cancellationToken)
    {
        if (!SetActor(context, tenantContext, out var actorId)) return InvalidToken();
        return Results.Ok(await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, offerId,
            context.Request.Headers["Idempotency-Key"].ToString(), System.Diagnostics.Activity.Current?.Id), cancellationToken));
    }

    private static async Task<IResult> DeleteOfferAsync(Guid offerId, HttpContext context,
        ITenantContext tenantContext, IDeleteOffer useCase, CancellationToken cancellationToken)
    {
        if (!SetActor(context, tenantContext, out var actorId)) return InvalidToken();
        await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, offerId,
            context.Request.Headers["Idempotency-Key"].ToString()), cancellationToken);
        return Results.NoContent();
    }

    private static bool SetActor(HttpContext context, ITenantContext tenantContext, out Guid actorId)
    {
        actorId = Guid.Empty;
        return SetTenant(context, tenantContext)
            && Guid.TryParse(context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty;
    }

    private static async Task<IResult> GetAsync(Guid courseId, HttpContext context, ITenantContext tenantContext,
        IGetCatalogCourse useCase, CancellationToken cancellationToken)
    {
        if (!SetTenant(context, tenantContext)) return InvalidToken();
        return Results.Ok(await useCase.ExecuteAsync(courseId, cancellationToken));
    }

    private static async Task<IResult> UpdateAsync(Guid courseId, JsonElement body, HttpContext context,
        ITenantContext tenantContext, IUpdateCatalogCourse useCase, CancellationToken cancellationToken)
    {
        if (!SetTenant(context, tenantContext)
            || !Guid.TryParse(context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId)
            || actorId == Guid.Empty) return InvalidToken();
        return Results.Ok(await useCase.ExecuteAsync(new(tenantContext.TenantId!.Value, actorId, courseId,
            context.Request.Headers["Idempotency-Key"].ToString(), body), cancellationToken));
    }

    private static bool SetTenant(HttpContext context, ITenantContext tenantContext)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty) return false;
        tenantContext.Set(tenantId);
        return true;
    }

    private static IResult InvalidToken() => Results.Problem(statusCode: 401, title: "Access token is invalid.",
        extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });

    private static async Task<IResult> ListAsync(HttpContext context, ITenantContext tenantContext,
        IListCatalogCourses useCase, CancellationToken cancellationToken, int _page = 1, int _size = 10)
    {
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty)
            return Results.Problem(statusCode: 401, title: "Access token tenant is invalid.",
                extensions: new Dictionary<string, object?> { ["code"] = "TOKEN_INVALID" });
        tenantContext.Set(tenantId);
        return Results.Ok(await useCase.ExecuteAsync(new ListCatalogCoursesInput(_page, _size), cancellationToken));
    }
}
