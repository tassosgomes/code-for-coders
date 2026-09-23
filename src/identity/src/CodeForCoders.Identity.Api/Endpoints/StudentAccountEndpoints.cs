using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Contracts;

namespace CodeForCoders.Identity.Api.Endpoints;

public static class StudentAccountEndpoints
{
    private const string RegistrationScope = "student-accounts:create";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/internal/v1/student-accounts", RegisterStudentAccountAsync)
            .WithName("CreateStudentAccountInternal")
            .WithTags("StudentIdentity")
            .Accepts<StudentRegistrationRequestV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> RegisterStudentAccountAsync(
        HttpContext httpContext,
        ServiceAssertionVerifier assertionVerifier,
        IRegisterStudentAccount useCase,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(
                httpContext.Request.Headers.Authorization.FirstOrDefault(),
                out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is required.");
        }

        var assertion = await assertionVerifier.VerifyAsync(
            authorization.Parameter,
            RegistrationScope,
            cancellationToken);
        if (assertion is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SERVICE_UNAUTHORIZED", "Service authentication is invalid.");
        }

        StudentRegistrationRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentRegistrationRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        if (request?.Name is null || request.Email is null || request.Password is null)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Name, email, and password are required.");
        }

        await useCase.ExecuteAsync(
            new RegisterStudentAccountInput(
                assertion.TenantId,
                request.Name,
                request.Email,
                request.Password,
                httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty),
            cancellationToken);
        return Results.Accepted();
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
