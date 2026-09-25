using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class StaffInvitationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffInvitationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/staff-invitations", ListPendingStaffInvitationsAsync)
            .WithName("ListPendingStaffInvitations")
            .WithTags("StaffInvitation")
            .Produces<StaffInvitationPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/api/v1/staff-invitations", CreateStaffInvitationAsync)
            .WithName("CreateStaffInvitation")
            .WithTags("StaffInvitation")
            .Accepts<CreateStaffInvitationRequestV1>("application/json")
            .Produces<StaffInvitationCreatedV1>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> ListPendingStaffInvitationsAsync(
        HttpContext httpContext,
        IStaffInvitationIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        var permission = CheckPermission(httpContext);
        if (permission is not null)
        {
            return permission;
        }

        if (!TryReadPage(httpContext.Request.Query["page"], 1, out var page)
            || !TryReadPage(httpContext.Request.Query["size"], 10, out var size)
            || size > 100)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Page and size are outside the supported range.");
        }

        var session = BffSessionContext.Get(httpContext)!;
        var result = await identityClient.ListPendingInvitationsAsync(
            session.IdentitySessionId,
            page,
            size,
            cancellationToken);
        return result.StatusCode == StatusCodes.Status200OK && result.Page is not null
            ? Results.Ok(result.Page)
            : ToProblem(httpContext, result);
    }

    private static async Task<IResult> CreateStaffInvitationAsync(
        HttpContext httpContext,
        IStaffInvitationIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        var permission = CheckPermission(httpContext);
        if (permission is not null)
        {
            return permission;
        }

        CreateStaffInvitationRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateStaffInvitationRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Email is null
            || string.IsNullOrWhiteSpace(request.Role)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email, role, and Idempotency-Key are required.");
        }

        var session = BffSessionContext.Get(httpContext)!;
        var result = await identityClient.CreateInvitationAsync(
            request,
            session.IdentitySessionId,
            idempotencyKey,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status201Created && result.Created is not null)
        {
            return Results.Created(
                $"/api/v1/staff-invitations/{result.Created.InvitationId:D}",
                result.Created);
        }

        return ToProblem(httpContext, result);
    }

    private static IResult? CheckPermission(HttpContext httpContext)
    {
        var session = BffSessionContext.Get(httpContext);
        if (session is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        var validated = BffSessionContext.GetValidatedSession(httpContext);
        if (validated is null
            || !validated.Permissions.Contains("acesso.gerir", StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff session cannot manage access.");
        }

        return null;
    }

    private static bool TryReadPage(string? value, int defaultValue, out int page)
    {
        if (value is null)
        {
            page = defaultValue;
            return true;
        }

        return int.TryParse(value, out page) && page > 0;
    }

    private static IResult ToProblem(HttpContext httpContext, StaffInvitationIdentityResult result)
    {
        var code = result.Code switch
        {
            "IDEMPOTENCY_CONFLICT" => "IDEMPOTENCY_KEY_REUSED",
            _ => result.Code ?? "IDENTITY_UNAVAILABLE",
        };
        var title = code switch
        {
            "EMAIL_BELONGS_TO_STAFF" => "Este e-mail já pertence a uma conta interna.",
            "EMAIL_BELONGS_TO_STUDENT" => "Este e-mail já pertence a uma conta de aluno.",
            "REASON_REQUIRED" => "Informe o motivo do convite.",
            "PERMISSION_DENIED" => "O ator não tem permissão para gerenciar acessos.",
            _ when code == "IDENTITY_UNAVAILABLE" => "Identity is temporarily unavailable.",
            _ => "The invitation request was rejected.",
        };
        return Problem(httpContext, result.StatusCode, code, title);
    }

    private static IResult Problem(HttpContext httpContext, int statusCode, string code, string title)
        => Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            });
}
