using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class AuditRecordEndpoints
{
    private const string AdministratorRole = "administrador";
    private const string AuditAudience = "audit";
    private const int IdentityReferenceBatchSize = 50;
    private static readonly string[] IdentityReferenceTypes = ["conta-interna", "convite-interno"];

    public static void MapAuditRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/audit-record-searches", SearchAsync)
            .WithName("ListAuditRecords")
            .WithTags("AuditRecords")
            .Accepts<AuditRecordSearchRequestV1>("application/json")
            .Produces<AuditRecordPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> SearchAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        IAuditRecordClient auditClient,
        IAuditIdentityReferenceClient identityReferenceClient,
        CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(httpContext);
        var currentSession = BffSessionContext.GetValidatedSession(httpContext);
        if (session is null || currentSession is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        if (!currentSession.Roles.Contains(AdministratorRole, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff role cannot read audit records.");
        }

        var request = await ReadRequestAsync(httpContext, cancellationToken);
        if (request is null
            || request.Page < 1
            || request.Size is < 1 or > 50
            || (request.Type is not null && string.IsNullOrWhiteSpace(request.Type)))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Audit record search request is invalid.");
        }

        var validation = await identityClient.ValidateSessionAsync(
            session.IdentitySessionId,
            AuditAudience,
            cancellationToken);
        if (validation.StatusCode == StatusCodes.Status401Unauthorized && validation.Code == "SESSION_REQUIRED")
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        if (validation.StatusCode != StatusCodes.Status200OK
            || validation.Session is null
            || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
        {
            var status = validation.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            return Problem(httpContext, status, "IDENTITY_UNAVAILABLE", "The staff identity service is temporarily unavailable.");
        }

        if (!validation.Session.Roles.Contains(AdministratorRole, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The current staff role cannot read audit records.");
        }

        var audit = await auditClient.SearchAsync(request, validation.Session.AccessToken, cancellationToken);
        if (audit.StatusCode != StatusCodes.Status200OK || audit.Page is null)
        {
            var title = audit.StatusCode == StatusCodes.Status401Unauthorized
                ? "The staff token is invalid."
                : audit.StatusCode == StatusCodes.Status403Forbidden
                    ? "The current staff role cannot read audit records."
                    : audit.StatusCode == StatusCodes.Status422UnprocessableEntity
                        ? "The audit search snapshot is invalid or expired."
                        : "The audit service is temporarily unavailable.";
            return Problem(httpContext, audit.StatusCode, audit.Code ?? "AUDIT_UNAVAILABLE", title);
        }

        var references = audit.Page.Data
            .SelectMany(record => new[] { record.Author, record.Target })
            .Where(reference => reference is not null
                && reference.Id != Guid.Empty
                && IdentityReferenceTypes.Contains(reference.Type, StringComparer.Ordinal))
            .Select(reference => new AuditIdentityReferenceV1(reference!.Type, reference.Id))
            .DistinctBy(reference => (reference.Type, reference.Id))
            .ToArray();
        if (references.Length == 0)
        {
            return Results.Ok(audit.Page);
        }

        var labels = new Dictionary<(string Type, Guid Id), string>();
        for (var index = 0; index < references.Length; index += IdentityReferenceBatchSize)
        {
            var batch = references.Skip(index).Take(IdentityReferenceBatchSize).ToArray();
            var lookup = await identityReferenceClient.ResolveAsync(session.IdentitySessionId, batch, cancellationToken);
            if (lookup.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            {
                var code = lookup.Code is "SERVICE_UNAUTHORIZED" or "SESSION_REQUIRED" or "PERMISSION_DENIED"
                    ? lookup.Code
                    : lookup.StatusCode == StatusCodes.Status401Unauthorized ? "SESSION_REQUIRED" : "PERMISSION_DENIED";
                return Problem(
                    httpContext,
                    lookup.StatusCode,
                    code,
                    lookup.StatusCode == StatusCodes.Status401Unauthorized
                        ? "A current administrator session is required."
                        : "The current staff role cannot read audit references.");
            }

            if (lookup.StatusCode != StatusCodes.Status200OK || lookup.Data is null)
            {
                return Results.Ok(audit.Page);
            }

            foreach (var reference in lookup.Data)
            {
                if (!string.IsNullOrWhiteSpace(reference.Label))
                {
                    labels[(reference.Type, reference.Id)] = reference.Label;
                }
            }
        }

        return Results.Ok(audit.Page with
        {
            Data = audit.Page.Data.Select(record => record with
            {
                Author = AddLabel(record.Author, labels),
                Target = AddLabel(record.Target, labels),
            }).ToArray(),
        });
    }

    private static AuditRecordIdentityReferenceV1? AddLabel(
        AuditRecordIdentityReferenceV1? reference,
        IReadOnlyDictionary<(string Type, Guid Id), string> labels)
        => reference is null || !labels.TryGetValue((reference.Type, reference.Id), out var label)
            ? reference
            : reference with { Label = label };

    private static async Task<AuditRecordSearchRequestV1?> ReadRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await System.Text.Json.JsonSerializer.DeserializeAsync<AuditRecordSearchRequestV1>(
                httpContext.Request.Body,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
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
