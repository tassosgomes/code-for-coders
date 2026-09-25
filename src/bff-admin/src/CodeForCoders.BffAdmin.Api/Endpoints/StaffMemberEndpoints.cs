using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class StaffMemberEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedRoles = ["professor", "suporte", "financeiro", "administrador"];

    public static void MapStaffMemberEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/staff-members", ListStaffMembersAsync)
            .WithName("ListStaffMembers")
            .WithTags("StaffMembers")
            .Produces<StaffMemberPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        app.MapPost("/api/v1/staff-members/{accountId:guid}/role-grants", GrantStaffRoleAsync)
            .WithName("GrantStaffRole")
            .WithTags("StaffMembers")
            .Accepts<StaffRoleActionRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        app.MapPost("/api/v1/staff-members/{accountId:guid}/role-revocations", RevokeStaffRoleAsync)
            .WithName("RevokeStaffRole")
            .WithTags("StaffMembers")
            .Accepts<StaffRoleActionRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        app.MapPost("/api/v1/staff-members/{accountId:guid}/role-changes", ChangeStaffRoleAsync)
            .WithName("ChangeStaffRole")
            .WithTags("StaffMembers")
            .Accepts<StaffRoleChangeRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> ListStaffMembersAsync(
        HttpContext httpContext,
        IStaffMemberIdentityClient identityClient,
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
        var result = await identityClient.ListStaffMembersAsync(
            session.IdentitySessionId,
            page,
            size,
            cancellationToken);
        return result.StatusCode == StatusCodes.Status200OK && result.Page is not null
            ? Results.Ok(result.Page)
            : ToProblem(httpContext, result);
    }

    private static Task<IResult> GrantStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        IStaffMemberIdentityClient identityClient,
        CancellationToken cancellationToken)
        => ExecuteRoleActionAsync(httpContext, accountId, identityClient, revoke: false, cancellationToken);

    private static Task<IResult> RevokeStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        IStaffMemberIdentityClient identityClient,
        CancellationToken cancellationToken)
        => ExecuteRoleActionAsync(httpContext, accountId, identityClient, revoke: true, cancellationToken);

    private static Task<IResult> ChangeStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        IStaffMemberIdentityClient identityClient,
        CancellationToken cancellationToken)
        => ExecuteRoleChangeAsync(httpContext, accountId, identityClient, cancellationToken);

    private static async Task<IResult> ExecuteRoleActionAsync(
        HttpContext httpContext,
        Guid accountId,
        IStaffMemberIdentityClient identityClient,
        bool revoke,
        CancellationToken cancellationToken)
    {
        var permission = CheckPermission(httpContext);
        if (permission is not null)
        {
            return permission;
        }

        StaffRoleActionRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffRoleActionRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (accountId == Guid.Empty
            || request?.Role is null
            || !SupportedRoles.Contains(request.Role, StringComparer.Ordinal)
            || request.Reason is null
            || request.Reason.Length > 1000
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Role, a reason within the supported length, and a valid Idempotency-Key are required.");
        }

        var session = BffSessionContext.Get(httpContext)!;
        var result = revoke
            ? await identityClient.RevokeRoleAsync(accountId, request, session.IdentitySessionId, idempotencyKey, cancellationToken)
            : await identityClient.GrantRoleAsync(accountId, request, session.IdentitySessionId, idempotencyKey, cancellationToken);
        return result.StatusCode == StatusCodes.Status200OK && result.Action is not null
            ? Results.Ok(result.Action)
            : ToProblem(httpContext, result);
    }

    private static async Task<IResult> ExecuteRoleChangeAsync(
        HttpContext httpContext,
        Guid accountId,
        IStaffMemberIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        var permission = CheckPermission(httpContext);
        if (permission is not null)
        {
            return permission;
        }

        StaffRoleChangeRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffRoleChangeRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (accountId == Guid.Empty
            || request?.FromRole is null
            || !SupportedRoles.Contains(request.FromRole, StringComparer.Ordinal)
            || request.ToRole is null
            || !SupportedRoles.Contains(request.ToRole, StringComparer.Ordinal)
            || request.Reason is null
            || request.Reason.Length > 1000
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Source and destination roles, a reason within the supported length, and a valid Idempotency-Key are required.");
        }

        var session = BffSessionContext.Get(httpContext)!;
        var result = await identityClient.ChangeRoleAsync(
            accountId,
            request,
            session.IdentitySessionId,
            idempotencyKey,
            cancellationToken);
        return result.StatusCode == StatusCodes.Status200OK && result.Action is not null
            ? Results.Ok(result.Action)
            : ToProblem(httpContext, result);
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

    private static IResult ToProblem(HttpContext httpContext, StaffMemberIdentityResult result)
    {
        var code = result.Code switch
        {
            "IDEMPOTENCY_CONFLICT" => "IDEMPOTENCY_KEY_REUSED",
            _ => result.Code ?? "IDENTITY_UNAVAILABLE",
        };
        var title = code switch
        {
            "STAFF_MEMBER_NOT_FOUND" => "A conta interna não foi encontrada.",
            "REASON_REQUIRED" => "Informe o motivo da alteração.",
            "SELF_ROLE_CHANGE_FORBIDDEN" => "Você não pode alterar os próprios papéis.",
            "ROLE_NOT_HELD" => "A conta não tem o papel que você escolheu remover.",
            "ROLE_CHANGE_INVALID" => "Escolha papéis de origem e destino diferentes.",
            "IDEMPOTENCY_KEY_REUSED" => "Chave de idempotência já usada com outro conteúdo.",
            "PERMISSION_DENIED" => "O ator não tem permissão para gerenciar acessos.",
            "IDENTITY_UNAVAILABLE" => "Identity is temporarily unavailable.",
            _ => "The staff role action was rejected.",
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
