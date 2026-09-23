using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class StudentPasswordRecoveryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentPasswordRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/password-reset-requests", RequestStudentPasswordResetAsync)
            .WithName("RequestStudentPasswordReset")
            .WithTags("Student")
            .Accepts<StudentPasswordResetRequestV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        endpoints.MapPost("/api/v1/password-resets", ResetStudentPasswordAsync)
            .WithName("ResetStudentPassword")
            .WithTags("Student")
            .Accepts<StudentPasswordResetV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> RequestStudentPasswordResetAsync(
        HttpContext httpContext,
        IStudentPasswordRecoveryIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StudentPasswordResetRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordResetRequestV1>(
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

        var result = await identityClient.RequestPasswordResetAsync(request, idempotencyKey, cancellationToken);
        if (result.StatusCode == StatusCodes.Status202Accepted)
        {
            return Results.Accepted();
        }

        var (code, title) = GetPublicProblem(result.Code);
        return Problem(httpContext, result.StatusCode, code, title);
    }

    private static async Task<IResult> ResetStudentPasswordAsync(
        HttpContext httpContext,
        IStudentPasswordRecoveryIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StudentPasswordResetV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordResetV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(request?.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Token, new password, and Idempotency-Key are required.");
        }

        var result = await identityClient.ResetPasswordAsync(request, idempotencyKey, cancellationToken);
        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return Results.NoContent();
        }

        var (code, title) = GetPublicProblem(result.Code);
        return Problem(httpContext, result.StatusCode, code, title);
    }

    private static (string Code, string Title) GetPublicProblem(string? code)
        => code switch
        {
            "PASSWORD_RESET_REJECTED" => (code, "The reset link or new password is invalid."),
            "IDEMPOTENCY_CONFLICT" => (code, "The idempotency key was already used with a different request."),
            "INVALID_REQUEST" => (code, "The password reset request is invalid."),
            _ => ("IDENTITY_UNAVAILABLE", "The student identity service is temporarily unavailable."),
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
