using System.Text.Json;
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
        group.MapPost("/{courseId:guid}/versions", PublishAsync);
        group.MapPatch("/{courseId:guid}", UpdateCourseAsync);
        group.MapPost("/{courseId:guid}/modules", CreateModuleAsync);
        group.MapPatch("/{courseId:guid}/modules/{moduleId:guid}", UpdateModuleAsync);
        group.MapDelete("/{courseId:guid}/modules/{moduleId:guid}", DeleteModuleAsync);
        group.MapPost("/{courseId:guid}/modules/{moduleId:guid}/lessons", CreateLessonAsync);
        group.MapPatch("/{courseId:guid}/lessons/{lessonId:guid}", UpdateLessonAsync);
        group.MapDelete("/{courseId:guid}/lessons/{lessonId:guid}", DeleteLessonAsync);

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
        ICourseAuthoringClient learning, CourseVideoEnricher videos, CancellationToken cancellationToken)
        => SendCoreAsync(new CourseOperation($"internal/v1/courses/{courseId:D}", null), context, identity, learning, videos, cancellationToken);

    private static Task<IResult> CreateAsync(CourseCreateBody body, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation("internal/v1/courses", body, "POST"), context, identity, learning, cancellationToken);

    private static Task<IResult> PublishAsync(Guid courseId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId:D}/versions", body, "POST"), context, identity, learning, cancellationToken);

    private static Task<IResult> UpdateCourseAsync(Guid courseId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}", body, "PATCH"), context, identity, learning, cancellationToken);

    private static Task<IResult> CreateModuleAsync(Guid courseId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/modules", body, "POST"), context, identity, learning, cancellationToken);

    private static Task<IResult> UpdateModuleAsync(Guid courseId, Guid moduleId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/modules/{moduleId}", body, "PATCH"), context, identity, learning, cancellationToken);

    private static Task<IResult> DeleteModuleAsync(Guid courseId, Guid moduleId, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/modules/{moduleId}", null, "DELETE"), context, identity, learning, cancellationToken);

    private static Task<IResult> CreateLessonAsync(Guid courseId, Guid moduleId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/modules/{moduleId}/lessons", body, "POST"), context, identity, learning, cancellationToken);

    private static Task<IResult> UpdateLessonAsync(Guid courseId, Guid lessonId, JsonElement body, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/lessons/{lessonId}", body, "PATCH"), context, identity, learning, cancellationToken);

    private static Task<IResult> DeleteLessonAsync(Guid courseId, Guid lessonId, HttpContext context, IStaffSessionIdentityClient identity, ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendAsync(new CourseOperation($"internal/v1/courses/{courseId}/lessons/{lessonId}", null, "DELETE"), context, identity, learning, cancellationToken);

    private static Task<IResult> SendAsync(CourseOperation operation, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CancellationToken cancellationToken)
        => SendCoreAsync(operation, context, identity, learning, null, cancellationToken);

    private static async Task<IResult> SendCoreAsync(CourseOperation operation, HttpContext context, IStaffSessionIdentityClient identity,
        ICourseAuthoringClient learning, CourseVideoEnricher? videos, CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(context);
        var validated = BffSessionContext.GetValidatedSession(context);
        if (session is null || validated is null) return Problem(401, "SESSION_REQUIRED");
        var permission = operation.Method == "GET" ? "autoria.ler" : "autoria.editar";
        if (!validated.Permissions.Contains(permission, StringComparer.Ordinal)) return Problem(403, "PERMISSION_DENIED");
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (operation.Method != "GET" && (string.IsNullOrWhiteSpace(key) || key.Length > 128)) return Problem(400, "INVALID_REQUEST");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "learning", cancellationToken);
        if (validation.StatusCode == 401) return Problem(401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(validation.StatusCode == 504 ? 504 : 502, "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(permission, StringComparer.Ordinal)) return Problem(403, "PERMISSION_DENIED");
        var result = await learning.SendAsync(new CourseClientRequest(operation.Path, validation.Session.AccessToken,
            validation.Session.Name, key, operation.Body, operation.Method), cancellationToken);
        if (result.Status == 201 && result.Version is not null)
            return Results.Created($"/api/v1/courses/{result.Version.CourseId:D}/versions/{result.Version.VersionNumber}", result.Version);
        if (result.Status == 201 && result.Course is not null)
            return Results.Created(result.Location?.Replace("/internal/v1", "/api/v1", StringComparison.Ordinal) ?? $"/api/v1/courses/{result.Course.CourseId:D}", result.Course);
        if (result.Status == 200 && result.Page is not null) return Results.Ok(result.Page);
        if (result.Status == 200 && result.Course is not null)
            return Results.Ok(videos is null ? result.Course : await videos.EnrichAsync(result.Course, session.IdentitySessionId, cancellationToken));
        return Problem(result.Status, result.Code ?? "LEARNING_UNAVAILABLE", result.Errors, result.Pendencies);
    }

    private static IResult Problem(int status, string code, System.Text.Json.JsonElement? errors = null, JsonElement? pendencies = null)
        => Results.Problem(statusCode: status, title: "The course request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["errors"] = errors,
                ["pendencies"] = pendencies,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
            });
}
