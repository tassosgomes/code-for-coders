using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.LookupStudentAccount;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentAccountLookupEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static void MapStudentAccountLookupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/student-account-lookups", LookupAsync)
            .WithName("LookupStudentAccountInternal").WithTags("StudentAccounts")
            .Accepts<StudentAccountLookupRequestV1>("application/json")
            .Produces<StudentAccountDetails>().ProducesProblem(400).ProducesProblem(401)
            .ProducesProblem(403).ProducesProblem(404);
    }

    private static async Task<IResult> LookupAsync(HttpContext context, ServiceAssertionVerifier verifier,
        ILookupStudentAccount useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(401, "SERVICE_UNAUTHORIZED");
        }

        var verification = await verifier.VerifyDetailedAsync(authorization.Parameter, "student-account:lookup", cancellationToken);
        if (verification.Assertion is null) return Problem(401, "SERVICE_UNAUTHORIZED");
        if (verification.Assertion.Issuer != "bff-admin" || !verification.ScopeGranted) return Problem(403, "PERMISSION_DENIED");
        if (!Guid.TryParse(context.Request.Headers["X-Staff-Session"], out var sessionId) || sessionId == Guid.Empty)
        {
            return Problem(401, "SESSION_REQUIRED");
        }

        var request = await ReadAsync(context, cancellationToken);
        // Authorization in the use case precedes body validation, including malformed JSON.
        var result = await useCase.ExecuteAsync(new LookupStudentAccountInput(verification.Assertion.TenantId, sessionId, request?.Email), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<StudentAccountLookupRequestV1?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<StudentAccountLookupRequestV1>(context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult Problem(int status, string code)
        => Results.Problem(statusCode: status, title: "Student account lookup rejected.", extensions: new Dictionary<string, object?> { ["code"] = code });
}
