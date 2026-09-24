using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class StudentConfirmationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentConfirmationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/account-confirmations", ConfirmStudentAccountAsync)
            .WithName("ConfirmStudentAccount")
            .WithTags("Student")
            .Accepts<StudentAccountConfirmationTokenV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapPost("/api/v1/account-confirmation-requests", RequestStudentAccountConfirmationAsync)
            .WithName("RequestStudentAccountConfirmation")
            .WithTags("Student")
            .Accepts<StudentAccountConfirmationEmailV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> ConfirmStudentAccountAsync(
        HttpContext httpContext,
        IStudentRegistrationIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StudentAccountConfirmationTokenV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentAccountConfirmationTokenV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Token) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Token and Idempotency-Key are required.");
        }

        var result = await identityClient.ConfirmAsync(request, idempotencyKey, cancellationToken);
        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return Results.NoContent();
        }

        var (code, title) = GetPublicProblem(result.Code);
        return Problem(httpContext, result.StatusCode, code, title);
    }

    private static async Task<IResult> RequestStudentAccountConfirmationAsync(
        HttpContext httpContext,
        IStudentRegistrationIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StudentAccountConfirmationEmailV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentAccountConfirmationEmailV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Email)
            || !new EmailAddressAttribute().IsValid(request.Email)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email and Idempotency-Key are required.");
        }

        var result = await identityClient.RequestConfirmationAsync(request, idempotencyKey, cancellationToken);
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
            "INVALID_VERIFICATION_TOKEN" => ("CONFIRMATION_LINK_INVALID", "The confirmation link is invalid or expired."),
            "IDEMPOTENCY_CONFLICT" => (code, "The idempotency key was already used with a different request."),
            "INVALID_REQUEST" => (code, "The confirmation request is invalid."),
            _ => ("IDENTITY_UNAVAILABLE", "The confirmation service is temporarily unavailable."),
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
