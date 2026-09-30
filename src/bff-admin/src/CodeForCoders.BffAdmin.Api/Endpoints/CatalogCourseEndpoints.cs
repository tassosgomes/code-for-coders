using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CatalogCourseEndpoints
{
    private const string EditOffers = "oferta.editar";

    public static void MapCatalogCourseEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("/api/v1/catalog").WithTags("Catalog")
            .MapGet("/courses", ListAsync).WithName("listCatalogCourses");

    private static async Task<IResult> ListAsync(HttpContext context, IStaffSessionIdentityClient identity,
        ICommerceCatalogClient commerce, CancellationToken cancellationToken, int _page = 1, int _size = 10)
    {
        var session = BffSessionContext.Get(context);
        var current = BffSessionContext.GetValidatedSession(context);
        if (session is null || current is null) return Problem(context, 401, "SESSION_REQUIRED");
        if (!current.Permissions.Contains(EditOffers, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "commerce", cancellationToken);
        if (validation.StatusCode == 401) return Problem(context, 401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(context, validation.StatusCode == 504 ? 504 : 502, "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(EditOffers, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue) return Problem(context, 400, "INVALID_REQUEST");
        var result = await commerce.ListAsync(new(validation.Session.AccessToken, _page, _size), cancellationToken);
        return result.Page is { } page ? Results.Ok(page) : Problem(context, result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE");
    }

    private static IResult Problem(HttpContext context, int status, string code)
        => Results.Problem(statusCode: status, title: "Catalog request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            });
}
