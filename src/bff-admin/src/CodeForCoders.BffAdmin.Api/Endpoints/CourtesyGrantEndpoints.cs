using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class CourtesyGrantEndpoints
{
    private const string Permission = "cortesia.conceder";
    public static void MapCourtesyGrantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/courtesy-grants", GrantAsync).WithName("grantCourtesy");
        endpoints.MapGet("/api/v1/courtesy-term-preview", PreviewAsync).WithName("previewCourtesyTerm");
        endpoints.MapGet("/api/v1/courtesy-grants/{grantId:guid}", GetAsync).WithName("getCourtesyGrant");
        endpoints.MapGet("/api/v1/students/{studentId:guid}/access-grants", ListStudentGrantsAsync).WithName("listStudentAccessGrants");
    }
    private static Task<IResult> GrantAsync(JsonElement body, HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyGrantsClient commerce, CancellationToken cancellationToken)
        => SendAsync(new("courtesy-grants", body), context, identity, commerce, cancellationToken);
    private static Task<IResult> GetAsync(Guid grantId, HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyGrantsClient commerce, CancellationToken cancellationToken)
        => SendAsync(new($"courtesy-grants/{grantId:D}", null), context, identity, commerce, cancellationToken);
    private static Task<IResult> PreviewAsync(int months, HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyGrantsClient commerce, CancellationToken cancellationToken)
        => months is < 1 or > 60 ? Task.FromResult(Problem(context, 400, "INVALID_REQUEST"))
            : SendAsync(new($"courtesy-term-preview?months={months}", null), context, identity, commerce, cancellationToken);

    private static Task<IResult> ListStudentGrantsAsync(Guid studentId, HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyGrantsClient commerce, CancellationToken cancellationToken, int _page = 1, int _size = 10)
        => _page < 1 || _size is < 1 or > 50 || (long)(_page - 1) * _size > int.MaxValue
            ? Task.FromResult(Problem(context, 400, "INVALID_REQUEST"))
            : SendAsync(new($"students/{studentId:D}/access-grants?_page={_page}&_size={_size}", null), context, identity, commerce, cancellationToken);

    private sealed record Request(string Path, JsonElement? Body);
    private static async Task<IResult> SendAsync(Request input, HttpContext context, IStaffSessionIdentityClient identity,
        ICourtesyGrantsClient commerce, CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(context);
        var current = BffSessionContext.GetValidatedSession(context);
        if (session is null || current is null) return Problem(context, 401, "SESSION_REQUIRED");
        if (!current.Permissions.Contains(Permission, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var key = input.Body is null ? null : context.Request.Headers["Idempotency-Key"].ToString();
        if (input.Body is not null && (string.IsNullOrWhiteSpace(key) || key.Length > 128)) return Problem(context, 400, "INVALID_REQUEST");
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "commerce", cancellationToken);
        if (validation.StatusCode == 401) return Problem(context, 401, "SESSION_REQUIRED");
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return Problem(context, validation.StatusCode == 504 ? 504 : 502, validation.StatusCode == 504 ? "UPSTREAM_TIMEOUT" : "IDENTITY_UNAVAILABLE");
        if (!validation.Session.Permissions.Contains(Permission, StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var result = await commerce.SendAsync(new(validation.Session.AccessToken, input.Path, input.Body, key), cancellationToken);
        if (result.Record is { } record) return result.StatusCode == 201
            ? Results.Created($"/api/v1/courtesy-grants/{record.GetProperty("grantId").GetGuid():D}", record) : Results.Ok(record);
        return Problem(context, result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE", result.Detail);
    }
    private static IResult Problem(HttpContext context, int status, string code, string? detail = null) => Results.Problem(statusCode: status,
        title: "Courtesy request could not be completed.", detail: detail, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier });
}
