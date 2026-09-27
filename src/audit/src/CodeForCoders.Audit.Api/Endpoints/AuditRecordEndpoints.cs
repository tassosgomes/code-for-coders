using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Audit.Api.ApiModels;
using CodeForCoders.Audit.Api.Configuration;
using CodeForCoders.Audit.Application.Exceptions;
using CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Audit.Api.Endpoints;

public static class AuditRecordEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static void MapAuditRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/audit-record-searches", SearchAsync)
            .RequireAuthorization()
            .WithName("ListAuditRecordsInternal")
            .WithTags("AuditRecords")
            .Accepts<AuditRecordSearchRequestV1>("application/json")
            .Produces<AuditRecordPageV1>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> SearchAsync(
        HttpContext httpContext,
        ISearchAuditRecords useCase,
        IOptions<AuditTokensOptions> tokenOptions,
        CancellationToken cancellationToken)
    {
        var identity = httpContext.User.Identity;
        if (identity?.IsAuthenticated != true
            || !Guid.TryParse(httpContext.User.FindFirstValue("tenantId"), out var tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(httpContext.User.FindFirstValue("sessionId"), out var sessionId)
            || sessionId == Guid.Empty)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "TOKEN_INVALID", "Staff token is invalid.");
        }

        if (!HasScope(httpContext.User, tokenOptions.Value.Scope))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "The staff token cannot read audit records.");
        }

        if (!httpContext.User.FindAll("roles").Any(claim => claim.Value == "administrador"))
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

        if (request.Snapshot is not null && !IsSnapshotToken(request.Snapshot))
        {
            return Problem(httpContext, StatusCodes.Status422UnprocessableEntity, "AUDIT_FILTER_INVALID", "The audit search snapshot is invalid or expired.");
        }

        try
        {
            var result = await useCase.ExecuteAsync(
                new SearchAuditRecordsInput(
                    tenantId,
                    sessionId,
                    request.Page,
                    request.Size,
                    request.Snapshot,
                    request.From,
                    request.To,
                    request.Type,
                    request.AuthorId,
                    request.TargetId,
                    request.Compliant),
                cancellationToken);
            return Results.Ok(new AuditRecordPageV1(
                result.Data.Select(ToApiModel).ToArray(),
                new AuditRecordPaginationV1(
                    result.Pagination.Page,
                    result.Pagination.Size,
                    result.Pagination.Total,
                    result.Pagination.TotalPages,
                    result.Pagination.Snapshot)));
        }
        catch (AuditFilterInvalidException)
        {
            return Problem(httpContext, StatusCodes.Status422UnprocessableEntity, "AUDIT_FILTER_INVALID", "The audit search snapshot is invalid or expired.");
        }
        catch (AuditSnapshotUnavailableException)
        {
            return Problem(httpContext, StatusCodes.Status503ServiceUnavailable, "AUDIT_QUERY_UNAVAILABLE", "The audit search is temporarily unavailable.");
        }
        catch (ValidationException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Audit record search request is invalid.");
        }
    }

    private static bool HasScope(ClaimsPrincipal principal, string requiredScope)
        => principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);

    private static AuditRecordSummaryV1 ToApiModel(AuditRecordSummaryOutput record)
        => new(
            record.Id,
            record.Type,
            record.PracticedAt,
            record.Author is null
                ? null
                : new AuditRecordIdentityReferenceV1(record.Author.Type, record.Author.Id),
            record.Target is null
                ? null
                : new AuditRecordIdentityReferenceV1(record.Target.Type, record.Target.Id),
            record.Compliant,
            record.HasComplements);

    private static async Task<AuditRecordSearchRequestV1?> ReadRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<AuditRecordSearchRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsSnapshotToken(string value)
        => value.Length == 48
            && value.StartsWith("snap_", StringComparison.Ordinal)
            && value.AsSpan(5).ToString().All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

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
