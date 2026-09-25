using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StaffMemberEndpoints
{
    private const string ReadScope = "staff-members:read";
    private const string WriteScope = "staff-members:write";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/staff-members", ListStaffMembersAsync)
            .WithName("ListStaffMembersInternal")
            .WithTags("StaffMembers")
            .Produces<StaffMemberPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/internal/v1/staff-members/{accountId:guid}/role-grants", GrantStaffRoleAsync)
            .WithName("GrantStaffRoleInternal")
            .WithTags("StaffMembers")
            .Accepts<StaffMemberRoleActionRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/staff-members/{accountId:guid}/role-revocations", RevokeStaffRoleAsync)
            .WithName("RevokeStaffRoleInternal")
            .WithTags("StaffMembers")
            .Accepts<StaffMemberRoleActionRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        endpoints.MapPost("/internal/v1/staff-members/{accountId:guid}/role-changes", ChangeStaffRoleAsync)
            .WithName("ChangeStaffRoleInternal")
            .WithTags("StaffMembers")
            .Accepts<StaffMemberRoleChangeRequestV1>("application/json")
            .Produces<StaffRoleActionResultV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ListStaffMembersAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IListStaffMembers useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, ReadScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        var actor = await ResolveActorAsync(httpContext, assertion.TenantId, sessionValidator, cancellationToken);
        if (actor.Problem is not null)
        {
            return actor.Problem;
        }

        if (!actor.Session!.Session.Permissions.Contains(StaffRoleCatalog.ManageAccess, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff session cannot manage access.");
        }

        if (!TryReadPage(httpContext.Request.Query["page"], 1, out var page)
            || !TryReadPage(httpContext.Request.Query["size"], 10, out var size)
            || size > 100)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Page and size are outside the supported range.");
        }

        var result = await useCase.ExecuteAsync(
            new ListStaffMembersInput(assertion.TenantId, actor.Session.Session.AccountId, page, size),
            cancellationToken);
        return Results.Ok(new StaffMemberPageV1(
            result.Data.Select(ToApiModel).ToArray(),
            new StaffMemberPaginationV1(
                result.Pagination.Page,
                result.Pagination.Size,
                result.Pagination.Total,
                result.Pagination.TotalPages)));
    }

    private static Task<IResult> GrantStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IGrantStaffRole useCase,
        CancellationToken cancellationToken)
        => ExecuteRoleActionAsync(
            httpContext,
            accountId,
            assertionVerifier,
            sessionValidator,
            request => useCase.ExecuteAsync(
                new GrantStaffRoleInput(
                    request.TenantId,
                    request.ActorAccountId,
                    accountId,
                    request.Role,
                    request.Reason,
                    request.IdempotencyKey),
                cancellationToken),
            cancellationToken);

    private static Task<IResult> RevokeStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IRevokeStaffRole useCase,
        CancellationToken cancellationToken)
        => ExecuteRoleActionAsync(
            httpContext,
            accountId,
            assertionVerifier,
            sessionValidator,
            request => useCase.ExecuteAsync(
                new RevokeStaffRoleInput(
                    request.TenantId,
                    request.ActorAccountId,
                    accountId,
                    request.Role,
                    request.Reason,
                    request.IdempotencyKey),
                cancellationToken),
            cancellationToken);

    private static Task<IResult> ChangeStaffRoleAsync(
        HttpContext httpContext,
        Guid accountId,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IChangeStaffRole useCase,
        CancellationToken cancellationToken)
        => ExecuteRoleChangeAsync(
            httpContext,
            accountId,
            assertionVerifier,
            sessionValidator,
            request => useCase.ExecuteAsync(
                new ChangeStaffRoleInput(
                    request.TenantId,
                    request.ActorAccountId,
                    accountId,
                    request.FromRole,
                    request.ToRole,
                    request.Reason,
                    request.IdempotencyKey),
                cancellationToken),
            cancellationToken);

    private static async Task<IResult> ExecuteRoleActionAsync(
        HttpContext httpContext,
        Guid accountId,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        Func<RoleActionRequest, Task<StaffRoleActionOutput>> executeAsync,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, WriteScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        if (accountId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A staff member account id is required.");
        }

        var actor = await ResolveActorAsync(httpContext, assertion.TenantId, sessionValidator, cancellationToken);
        if (actor.Problem is not null)
        {
            return actor.Problem;
        }

        if (!actor.Session!.Session.Permissions.Contains(StaffRoleCatalog.ManageAccess, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff session cannot manage access.");
        }

        var body = await ReadRequestAsync<StaffMemberRoleActionRequestV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (body?.Role is null
            || !StaffRoleCatalog.Contains(body.Role)
            || body.Reason is null
            || body.Reason.Length > 1000
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Role, a reason within the supported length, and a valid Idempotency-Key are required.");
        }

        try
        {
            var result = await executeAsync(new RoleActionRequest(
                assertion.TenantId,
                actor.Session.Session.AccountId,
                body.Role,
                body.Reason,
                idempotencyKey));
            return Results.Ok(new StaffRoleActionResultV1(
                ToApiModel(result.Member),
                result.Changed,
                result.SessionsEnded));
        }
        catch (StaffRoleActionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static async Task<IResult> ExecuteRoleChangeAsync(
        HttpContext httpContext,
        Guid accountId,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        Func<RoleChangeRequest, Task<StaffRoleActionOutput>> executeAsync,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, WriteScope, cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        if (accountId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A staff member account id is required.");
        }

        var actor = await ResolveActorAsync(httpContext, assertion.TenantId, sessionValidator, cancellationToken);
        if (actor.Problem is not null)
        {
            return actor.Problem;
        }

        if (!actor.Session!.Session.Permissions.Contains(StaffRoleCatalog.ManageAccess, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff session cannot manage access.");
        }

        var body = await ReadRequestAsync<StaffMemberRoleChangeRequestV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (body?.FromRole is null
            || !StaffRoleCatalog.Contains(body.FromRole)
            || body.ToRole is null
            || !StaffRoleCatalog.Contains(body.ToRole)
            || body.Reason is null
            || body.Reason.Length > 1000
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Source and destination roles, a reason within the supported length, and a valid Idempotency-Key are required.");
        }

        try
        {
            var result = await executeAsync(new RoleChangeRequest(
                assertion.TenantId,
                actor.Session.Session.AccountId,
                body.FromRole,
                body.ToRole,
                body.Reason,
                idempotencyKey));
            return Results.Ok(new StaffRoleActionResultV1(
                ToApiModel(result.Member),
                result.Changed,
                result.SessionsEnded));
        }
        catch (StaffRoleActionException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static StaffMemberV1 ToApiModel(StaffMemberOutput member)
        => new(member.AccountId, member.Name, member.Email, member.Roles, member.IsSelf);

    private static async Task<(ValidateStaffSessionOutput? Session, IResult? Problem)> ResolveActorAsync(
        HttpContext httpContext,
        Guid tenantId,
        IValidateStaffSession sessionValidator,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(httpContext.Request.Headers["X-Staff-Session"].FirstOrDefault(), out var sessionId)
            || sessionId == Guid.Empty)
        {
            return (null, Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "X-Staff-Session is required."));
        }

        var session = await sessionValidator.ExecuteAsync(
            new ValidateStaffSessionInput(tenantId, sessionId),
            cancellationToken);
        return session is null
            ? (null, Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required."))
            : (session, null);
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

    private static async Task<TRequest?> ReadRequestAsync<TRequest>(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<TRequest>(httpContext.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static async Task<VerifiedServiceAssertion?> VerifyServiceAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.FirstOrDefault(),
                out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await assertionVerifier.VerifyAsync(authorization.Parameter, requiredScope, cancellationToken);
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

    private sealed record RoleActionRequest(
        Guid TenantId,
        Guid ActorAccountId,
        string Role,
        string Reason,
        string IdempotencyKey);

    private sealed record RoleChangeRequest(
        Guid TenantId,
        Guid ActorAccountId,
        string FromRole,
        string ToRole,
        string Reason,
        string IdempotencyKey);
}
