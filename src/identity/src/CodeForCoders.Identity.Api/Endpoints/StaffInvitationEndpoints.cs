using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;
using CodeForCoders.Identity.Application.UseCases.Accounts.ListPendingStaffInvitations;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StaffInvitationEndpoints
{
    private const string ReadScope = "staff-invitations:read";
    private const string WriteScope = "staff-invitations:write";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/staff-invitations", ListPendingStaffInvitationsAsync)
            .WithName("ListPendingStaffInvitationsInternal")
            .WithTags("StaffInvitation")
            .Produces<StaffInvitationPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/internal/v1/staff-invitations", CreateStaffInvitationAsync)
            .WithName("CreateStaffInvitationInternal")
            .WithTags("StaffInvitation")
            .Accepts<CreateStaffInvitationRequestV1>("application/json")
            .Produces<StaffInvitationCreatedV1>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ListPendingStaffInvitationsAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IListPendingStaffInvitations useCase,
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

        try
        {
            var result = await useCase.ExecuteAsync(
                new ListPendingStaffInvitationsInput(assertion.TenantId, page, size),
                cancellationToken);
            return Results.Ok(new StaffInvitationPageV1(
                result.Data.Select(item => new PendingStaffInvitationV1(
                    item.InvitationId,
                    item.Email,
                    item.OfferedRole,
                    item.InvitedAt,
                    item.ExpiresAt)).ToArray(),
                new InvitationPaginationV1(
                    result.Pagination.Page,
                    result.Pagination.Size,
                    result.Pagination.Total,
                    result.Pagination.TotalPages)));
        }
        catch (StaffInvitationException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

    private static async Task<IResult> CreateStaffInvitationAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        ICreateStaffInvitation useCase,
        CancellationToken cancellationToken)
    {
        var assertion = await VerifyServiceAsync(httpContext, assertionVerifier, WriteScope, cancellationToken);
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

        var request = await ReadRequestAsync<CreateStaffInvitationRequestV1>(httpContext, cancellationToken);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Email is null
            || !new EmailAddressAttribute().IsValid(request.Email.Trim())
            || string.IsNullOrWhiteSpace(request.Role)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email, role, and Idempotency-Key are required.");
        }

        try
        {
            var created = await useCase.ExecuteAsync(
                new CreateStaffInvitationInput(
                    assertion.TenantId,
                    actor.Session.Session.AccountId,
                    request.Email,
                    request.Role,
                    request.Reason ?? string.Empty,
                    idempotencyKey),
                cancellationToken);
            return Results.Created(
                $"/internal/v1/staff-invitations/{created.InvitationId:D}",
                new StaffInvitationCreatedV1(
                    created.InvitationId,
                    created.Email,
                    created.OfferedRole,
                    created.InvitedAt,
                    created.ExpiresAt,
                    created.SupersededInvitationId));
        }
        catch (StaffInvitationException exception)
        {
            return Problem(httpContext, exception.StatusCode, exception.Code, exception.Title);
        }
    }

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
}
