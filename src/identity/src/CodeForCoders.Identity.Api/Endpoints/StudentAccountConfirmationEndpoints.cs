using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.ApiModels;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccountEligibility;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentAccountConfirmationEndpoints
{
    public static void MapStudentAccountConfirmationEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/internal/v1/student-account-confirmations", ConfirmAsync)
            .WithName("confirmStudentAccountInternal").WithTags("StudentAccounts");

    private static async Task<IResult> ConfirmAsync(HttpContext context, ServiceAssertionVerifier verifier,
        IConfirmStudentAccountEligibility useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)) return Problem(401);
        var verified = await verifier.VerifyDetailedAsync(authorization.Parameter, "student-account:confirm", cancellationToken);
        if (verified.Assertion is null) return Problem(401);
        if (verified.Assertion.Issuer != "commerce" || !verified.ScopeGranted) return Problem(403);
        StudentAccountConfirmationRequest? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<StudentAccountConfirmationRequest>(context.Request.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
        }
        catch (JsonException) { return Problem(400); }
        if (input is null || input.StudentId == Guid.Empty) return Problem(400);
        return Results.Ok(new { eligible = await useCase.ExecuteAsync(new(verified.Assertion.TenantId, input.StudentId), cancellationToken) });
    }
    private static IResult Problem(int status) => Results.Problem(statusCode: status, title: "Student account confirmation rejected.",
        extensions: new Dictionary<string, object?> { ["code"] = status == 401 ? "SERVICE_UNAUTHORIZED" : status == 403 ? "PERMISSION_DENIED" : "INVALID_REQUEST" });
}
