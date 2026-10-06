using System.Net.Http.Headers;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.GetStudentContact;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentContactEndpoints
{
    public static void MapStudentContactEndpoints(this IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/internal/v1/student-accounts/{studentId:guid}/contact", GetAsync)
            .WithName("getStudentContactInternal").WithTags("StudentAccounts");

    private static async Task<IResult> GetAsync(Guid studentId, HttpContext context, ServiceAssertionVerifier verifier,
        IGetStudentContact useCase, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)) return Problem(401);
        var verified = await verifier.VerifyDetailedAsync(authorization.Parameter, "student-contact:read", cancellationToken);
        if (verified.Assertion is null) return Problem(401);
        if (verified.Assertion.Issuer != "notification" || !verified.ScopeGranted) return Problem(403);
        var contact = await useCase.ExecuteAsync(new(verified.Assertion.TenantId, studentId), cancellationToken);
        return Results.Ok(contact);
    }

    private static IResult Problem(int status) => Results.Problem(statusCode: status,
        title: status == 404 ? "Conta de aluno não encontrada." : "Student contact request rejected.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = status == 404 ? "STUDENT_ACCOUNT_NOT_FOUND" : status == 401 ? "SERVICE_UNAUTHORIZED" : "PERMISSION_DENIED",
            ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("N")
        });
}
