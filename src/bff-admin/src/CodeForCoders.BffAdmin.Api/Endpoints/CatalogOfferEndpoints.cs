using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CatalogOfferEndpoints
{
    private const string EditOffers = "oferta.editar";
    public static void MapCatalogOfferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/catalog").WithTags("Catalog");
        group.MapPost("/courses/{courseId:guid}/offers", CreateAsync).WithName("createOffer");
        group.MapPatch("/offers/{offerId:guid}", UpdateAsync).WithName("updateOffer");
        group.MapPost("/offers/{offerId:guid}/publish", PublishAsync).WithName("publishOffer");
        group.MapPost("/offers/{offerId:guid}/unpublish", UnpublishAsync).WithName("unpublishOffer");
        group.MapDelete("/offers/{offerId:guid}", DeleteAsync).WithName("deleteOffer");
    }

    private static Task<IResult> CreateAsync(Guid courseId, JsonElement body, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => WriteAsync(new(courseId, body, HttpMethod.Post), context, identity, commerce, cancellationToken);

    private static Task<IResult> UpdateAsync(Guid offerId, JsonElement body, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => WriteAsync(new(offerId, body, HttpMethod.Patch), context, identity, commerce, cancellationToken);

    private static Task<IResult> DeleteAsync(Guid offerId, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => WriteAsync(new(offerId, null, HttpMethod.Delete), context, identity, commerce, cancellationToken);

    private static Task<IResult> PublishAsync(Guid offerId, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => WriteAsync(new(offerId, null, HttpMethod.Post, OfferTransition.Publish), context, identity, commerce, cancellationToken);

    private static Task<IResult> UnpublishAsync(Guid offerId, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => WriteAsync(new(offerId, null, HttpMethod.Post, OfferTransition.Unpublish), context, identity, commerce, cancellationToken);

    private enum OfferTransition { None, Publish, Unpublish }

    private sealed record WriteInput(Guid TargetId, JsonElement? Body, HttpMethod Method, OfferTransition Transition = OfferTransition.None);

    private static async Task<IResult> WriteAsync(WriteInput input, HttpContext context,
        IStaffSessionIdentityClient identity, ICommerceCatalogClient commerce, CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(context);
        var current = BffSessionContext.GetValidatedSession(context);
        if (session is null || current is null) return Problem(context, 401, "SESSION_REQUIRED");
        if (!current.Permissions.Contains(EditOffers, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128) return Problem(context, 400, "INVALID_REQUEST");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "commerce", cancellationToken);
        if (validation.StatusCode == 401) return Problem(context, 401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(context, validation.StatusCode == 504 ? 504 : 502, "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(EditOffers, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var request = new CatalogOfferRequest(validation.Session.AccessToken, input.TargetId, input.Body, key);
        var result = input.Transition == OfferTransition.Publish ? await commerce.PublishOfferAsync(request, cancellationToken)
            : input.Transition == OfferTransition.Unpublish ? await commerce.UnpublishOfferAsync(request, cancellationToken)
            : input.Method == HttpMethod.Post ? await commerce.CreateOfferAsync(request, cancellationToken)
            : input.Method == HttpMethod.Patch ? await commerce.UpdateOfferAsync(request, cancellationToken)
            : await commerce.DeleteOfferAsync(request, cancellationToken);
        if (result.StatusCode == 204) return Results.NoContent();
        if (result.Record is { } offer) return result.StatusCode == 201
            ? Results.Created($"/api/v1/catalog/offers/{offer.GetProperty("offerId").GetGuid():D}", offer) : Results.Ok(offer);
        return Problem(context, result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE", result.Detail);
    }

    private static IResult Problem(HttpContext context, int status, string code, string? detail = null)
        => Results.Problem(statusCode: status, title: "Offer request could not be completed.", detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
            });
}
