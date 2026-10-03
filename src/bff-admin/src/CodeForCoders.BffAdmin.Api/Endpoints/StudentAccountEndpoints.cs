using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class StudentAccountEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static void MapStudentAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/student-account-lookups", LookupAsync)
            .WithName("LookupStudentAccount").WithTags("StudentAccounts")
            .Accepts<StudentAccountLookupRequestV1>("application/json").Produces<StudentAccountV1>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(502).ProducesProblem(504);
    }

    private static async Task<IResult> LookupAsync(HttpContext context, IStudentAccountIdentityClient identityClient, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var session = BffSessionContext.Get(context);
        var validated = BffSessionContext.GetValidatedSession(context);
        if (session is null || validated is null) return Problem(context, 401, "SESSION_REQUIRED");
        if (!validated.Permissions.Contains("cortesia.conceder", StringComparer.Ordinal)) return Problem(context, 403, "PERMISSION_DENIED");
        var input = await ReadAsync(context, cancellationToken);
        if (input is null) return Problem(context, 400, "VALIDATION_ERROR");
        var result = await identityClient.LookupAsync(session.IdentitySessionId, input, cancellationToken);
        return result.StatusCode == 200 && result.Account is not null ? Results.Ok(result.Account)
            : Problem(context, result.StatusCode, result.Code ?? "IDENTITY_UNAVAILABLE");
    }

    private static async Task<StudentAccountLookupRequestV1?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        try { return await JsonSerializer.DeserializeAsync<StudentAccountLookupRequestV1>(context.Request.Body, JsonOptions, cancellationToken); }
        catch (JsonException) { return null; }
    }

    private static IResult Problem(HttpContext context, int status, string code)
        => Results.Problem(statusCode: status, title: "Student account lookup rejected.", extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        });
}
