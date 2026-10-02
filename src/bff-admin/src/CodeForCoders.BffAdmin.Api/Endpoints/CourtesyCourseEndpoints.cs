using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;
using System.Text.Json;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CourtesyCourseEndpoints
{
    private const string GrantPermission = "cortesia.conceder";

    public static void MapCourtesyCourseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/courtesy-courses", ListAsync).WithTags("Courtesies").WithName("listCourtesyCourses");
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyCoursesClient commerce, CancellationToken cancellationToken, int _page = 1, int _size = 10, string? title = null)
    {
        var session = BffSessionContext.Get(context);
        var current = BffSessionContext.GetValidatedSession(context);
        if (session is null || current is null) return Problem(context, 401, "SESSION_REQUIRED");
        if (!current.Permissions.Contains(GrantPermission, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "commerce", cancellationToken);
        if (validation.StatusCode == 401) return Problem(context, 401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(context, validation.StatusCode == 504 ? 504 : 502, validation.StatusCode == 504 ? "UPSTREAM_TIMEOUT" : "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(GrantPermission, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        if (title is not null && title.Length is < 1 or > 100) return Problem(context, 400, "INVALID_REQUEST");
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue) return Problem(context, 400, "INVALID_REQUEST");
        var result = await commerce.ListAsync(new(validation.Session.AccessToken, _page, _size, title), cancellationToken);
        return result.Page is { } page ? Results.Ok(page) : Problem(context, result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE");
    }

    private static IResult Problem(HttpContext context, int status, string code, string? detail = null)
        => Results.Problem(statusCode: status, title: "Courtesy course request could not be completed.", detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            });
}
