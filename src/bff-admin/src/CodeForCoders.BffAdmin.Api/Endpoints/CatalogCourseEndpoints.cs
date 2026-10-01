using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;
using System.Text.Json;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CatalogCourseEndpoints
{
    private const string EditOffers = "oferta.editar";

    public static void MapCatalogCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/catalog").WithTags("Catalog");
        group.MapGet("/courses", ListAsync).WithName("listCatalogCourses");
        group.MapGet("/courses/{courseId:guid}", GetAsync).WithName("getCatalogCourse");
        group.MapPatch("/courses/{courseId:guid}", UpdateAsync).WithName("updateCatalogCourse");
    }

    private static Task<IResult> GetAsync(Guid courseId, HttpContext context, IStaffSessionIdentityClient identity,
        ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => RecordAsync(new(courseId, null), context, identity, commerce, cancellationToken);

    private static Task<IResult> UpdateAsync(Guid courseId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity,
        ICommerceCatalogClient commerce, CancellationToken cancellationToken)
        => RecordAsync(new(courseId, body), context, identity, commerce, cancellationToken);

    private sealed record RecordInput(Guid CourseId, JsonElement? Body);

    private static async Task<IResult> RecordAsync(RecordInput input, HttpContext context, IStaffSessionIdentityClient identity,
        ICommerceCatalogClient commerce, CancellationToken cancellationToken)
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
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (input.Body.HasValue && (string.IsNullOrWhiteSpace(key) || key.Length > 128)) return Problem(context, 400, "INVALID_REQUEST");
        var request = new CatalogCourseRecordRequest(validation.Session.AccessToken, input.CourseId, input.Body, key);
        var result = input.Body.HasValue ? await commerce.UpdateAsync(request, cancellationToken) : await commerce.GetAsync(request, cancellationToken);
        return result.Record is { } record ? Results.Ok(record) : Problem(context, result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE", result.Detail);
    }

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

    private static IResult Problem(HttpContext context, int status, string code, string? detail = null)
        => Results.Problem(statusCode: status, title: "Catalog request could not be completed.", detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            });
}
