using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResolveAuditIdentityReferences;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class AuditIdentityReferenceEndpoints
{
    private const string Scope = "audit-references:read";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static void MapAuditIdentityReferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/audit-identity-reference-lookups", ResolveReferencesAsync)
            .WithName("ResolveAuditIdentityReferencesInternal")
            .WithTags("AuditIdentityReferences")
            .Accepts<AuditIdentityReferenceLookupRequestV1>("application/json")
            .Produces<AuditIdentityReferenceLookupResponseV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ResolveReferencesAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IValidateStaffSession sessionValidator,
        IResolveAuditIdentityReferences useCase,
        CancellationToken cancellationToken)
    {
        var verification = await VerifyServiceAsync(httpContext, assertionVerifier, cancellationToken);
        if (verification.Assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        if (!verification.ScopeGranted)
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The service is not allowed to resolve audit references.");
        }

        if (!Guid.TryParse(httpContext.Request.Headers["X-Staff-Session"].FirstOrDefault(), out var sessionId)
            || sessionId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current administrator session is required.");
        }

        var session = await sessionValidator.ExecuteAsync(
            new ValidateStaffSessionInput(verification.Assertion.TenantId, sessionId),
            cancellationToken);
        if (session is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current administrator session is required.");
        }

        if (!session.Session.Roles.Contains(StaffRoleCatalog.Administrator, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff session cannot read audit references.");
        }

        var request = await ReadRequestAsync(httpContext, cancellationToken);
        if (!IsValidRequest(request, out var references))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Audit reference lookup request is invalid.");
        }

        try
        {
            var result = await useCase.ExecuteAsync(
                new ResolveAuditIdentityReferencesInput(verification.Assertion.TenantId, references),
                cancellationToken);
            return Results.Ok(new AuditIdentityReferenceLookupResponseV1(result.Data
                .Select(reference => new ResolvedAuditIdentityReferenceV1(reference.Type, reference.Id, reference.Label))
                .ToArray()));
        }
        catch (ValidationException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Audit reference lookup request is invalid.");
        }
    }

    private static async Task<ServiceAssertionVerification> VerifyServiceAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.FirstOrDefault(),
                out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceAssertionVerification(null, false);
        }

        return await assertionVerifier.VerifyDetailedAsync(authorization.Parameter, Scope, cancellationToken);
    }

    private static async Task<AuditIdentityReferenceLookupRequestV1?> ReadRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<AuditIdentityReferenceLookupRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValidRequest(
        AuditIdentityReferenceLookupRequestV1? request,
        out IReadOnlyList<AuditIdentityReferenceKey> references)
    {
        references = [];
        if (request?.References is not { Count: > 0 and <= 50 })
        {
            return false;
        }

        var keys = new List<AuditIdentityReferenceKey>(request.References.Count);
        foreach (var reference in request.References)
        {
            if (reference.Id == Guid.Empty
                || reference.Type is not ("conta-interna" or "convite-interno" or "conta-aluno"))
            {
                return false;
            }

            keys.Add(new AuditIdentityReferenceKey(reference.Type, reference.Id));
        }

        if (keys.Distinct().Count() != keys.Count)
        {
            return false;
        }

        references = keys;
        return true;
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
