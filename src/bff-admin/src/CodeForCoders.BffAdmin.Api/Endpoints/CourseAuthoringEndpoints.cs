using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CourseAuthoringEndpoints
{
    public static void MapCourseAuthoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/courses").WithTags("Courses");
        group.MapGet("", ListAsync);
        group.MapGet("/{courseId:guid}", GetAsync);
        group.MapPost("", CreateAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken, int _page = 1, int _size = 20, string? status = null)
    {
        if (_page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue || status is not (null or "draft" or "published"))
            return Problem(400, "INVALID_REQUEST");
        var filters = status is null ? "" : $"&status={status}";
        return await SendAsync(new CourseOperation($"internal/v1/courses?_page={_page}&_size={_size}{filters}", null),
            context, identity, learning, cancellationToken);
    }

    private static Task<IResult> GetAsync(Guid courseId, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId:D}", null), context, identity, learning, cancellationToken);

    private static Task<IResult> CreateAsync(CourseCreateBody body, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation("internal/v1/courses", body), context, identity, learning, cancellationToken);

    private static async Task<IResult> SendAsync(CourseOperation operation, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(context);
        var validated = BffSessionContext.GetValidatedSession(context);
        if (session is null || validated is null) return Problem(401, "SESSION_REQUIRED");
        var permission = operation.Body is null ? "autoria.ler" : "autoria.editar";
        if (!validated.Permissions.Contains(permission, StringComparer.Ordinal)) return Problem(403, "PERMISSION_DENIED");
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (operation.Body is not null && (string.IsNullOrWhiteSpace(key) || key.Length > 128)) return Problem(400, "INVALID_REQUEST");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "learning", cancellationToken);
        if (validation.StatusCode == 401) return Problem(401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(validation.StatusCode == 504 ? 504 : 502, "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(permission, StringComparer.Ordinal)) return Problem(403, "PERMISSION_DENIED");
        var result = await learning.SendAsync(new CourseClientRequest(operation.Path, validation.Session.AccessToken,
            validation.Session.Name, key, operation.Body), cancellationToken);
        if (result.Status == 201 && result.Course is not null)
            return Results.Created($"/api/v1/courses/{result.Course.CourseId:D}", result.Course);
        if (result.Status == 200 && result.Page is not null) return Results.Ok(result.Page);
        if (result.Status == 200 && result.Course is not null) return Results.Ok(result.Course);
        return Problem(result.Status, result.Code ?? "LEARNING_UNAVAILABLE", result.Errors);
    }

    private static IResult Problem(int status, string code, System.Text.Json.JsonElement? errors = null)
        => Results.Problem(statusCode: status, title: "The course request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["errors"] = errors,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
            });
}
