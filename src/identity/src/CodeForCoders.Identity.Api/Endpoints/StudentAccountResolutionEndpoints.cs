using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.ResolveStudentAccounts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentAccountResolutionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static void MapStudentAccountResolutionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/student-account-resolutions", ResolveAsync)
            .WithName("ResolveStudentAccountsInternal").WithTags("StudentAccounts")
            .Accepts<StudentAccountResolutionRequest>("application/json")
            .Produces<StudentAccountResolutionList>().ProducesProblem(400).ProducesProblem(401)
            .ProducesProblem(403).ProducesProblem(404);
    }

    private static async Task<IResult> ResolveAsync(HttpContext context, ServiceAssertionVerifier verifier,
        IResolveStudentAccounts useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(401, "SERVICE_UNAUTHORIZED");
        }

        var verification = await verifier.VerifyDetailedAsync(authorization.Parameter, "student-account:resolve", cancellationToken);
        if (verification.Assertion is null) return Problem(401, "SERVICE_UNAUTHORIZED");
        if (verification.Assertion.Issuer != "bff-admin" || !verification.ScopeGranted) return Problem(403, "PERMISSION_DENIED");
        if (!Guid.TryParse(context.Request.Headers["X-Staff-Session"], out var sessionId) || sessionId == Guid.Empty)
        {
            return Problem(401, "SESSION_REQUIRED");
        }

        var request = await ReadAsync(context, cancellationToken);
        // Authorization in the use case precedes body validation, including malformed JSON.
        var result = await useCase.ExecuteAsync(new ResolveStudentAccountsInput(verification.Assertion.TenantId, sessionId, request?.StudentIds), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<StudentAccountResolutionRequest?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<StudentAccountResolutionRequest>(context.Request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IResult Problem(int status, string code)
        => Results.Problem(statusCode: status, title: "Student account resolution rejected.", extensions: new Dictionary<string, object?> { ["code"] = code });
}
