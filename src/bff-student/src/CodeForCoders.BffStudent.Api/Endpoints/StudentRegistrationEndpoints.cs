using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class StudentRegistrationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/student-accounts", RegisterStudentAsync)
            .WithName("RegisterStudent")
            .WithTags("Student")
            .Accepts<StudentRegistrationRequestV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> RegisterStudentAsync(
        HttpContext httpContext,
        IStudentRegistrationIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
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

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (request?.Name is null || request.Email is null || request.Password is null
            || string.IsNullOrWhiteSpace(request.Name)
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Name, email, password, and Idempotency-Key are required.");
        }

        var result = await identityClient.RegisterAsync(request, idempotencyKey, cancellationToken);
        if (result.StatusCode == StatusCodes.Status202Accepted)
        {
            return Results.Accepted();
        }

        var (code, title) = GetPublicProblem(result.Code);
        return Problem(httpContext, result.StatusCode, code, title);
    }

    private static (string Code, string Title) GetPublicProblem(string? code)
        => code switch
        {
            "ACCOUNT_ALREADY_EXISTS" => (code, "An active account already uses this email address."),
            "PASSWORD_POLICY_VIOLATION" => (code, "The password does not meet the account security policy."),
            "IDEMPOTENCY_CONFLICT" => (code, "The idempotency key was already used with a different request."),
            "INVALID_REQUEST" => (code, "The registration request is invalid."),
            _ => ("IDENTITY_UNAVAILABLE", "The registration service is temporarily unavailable."),
        };

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
