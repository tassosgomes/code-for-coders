using System.Text.Json;
using CodeForCoders.BffStudent.Api.ApiModels;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class StudentPasswordChangeEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStudentPasswordChangeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/password-changes", ChangeStudentPasswordAsync)
            .WithName("ChangeStudentPassword")
            .WithTags("Student")
            .Accepts<StudentPasswordChangeV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> ChangeStudentPasswordAsync(
        HttpContext httpContext,
        IStudentPasswordChangeIdentityClient identityClient,
        IBffSessionStore sessionStore,
        IOptions<BffSecurityOptions> securityOptions,
        CancellationToken cancellationToken)
    {
        StudentPasswordChangeV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StudentPasswordChangeV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var session = BffSessionContext.Get(httpContext);
        if (string.IsNullOrEmpty(request?.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Current password, new password, and Idempotency-Key are required.");
        }

        if (session is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current student session is required.");
        }

        var result = await identityClient.ChangePasswordAsync(
            session.StudentSessionId,
            request,
            idempotencyKey,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return Results.NoContent();
        }

        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            var cookieValue = httpContext.Request.Cookies[securityOptions.Value.SessionCookieName];
            if (!string.IsNullOrWhiteSpace(cookieValue))
            {
                await sessionStore.RemoveAsync(cookieValue, cancellationToken);
            }

            StudentSessionCookie.Delete(httpContext, securityOptions.Value);
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current student session is required.");
        }

        var (statusCode, code, title) = GetProblem(result);
        return Problem(httpContext, statusCode, code, title);
    }

    private static (int StatusCode, string Code, string Title) GetProblem(StudentPasswordChangeResult result)
        => result.Code switch
        {
            "PASSWORD_CHANGE_REJECTED" => (
                StatusCodes.Status422UnprocessableEntity,
                result.Code,
                "The current password is incorrect or the new password does not meet the policy."),
            "IDEMPOTENCY_CONFLICT" => (
                StatusCodes.Status422UnprocessableEntity,
                result.Code,
                "The idempotency key was already used with a different request."),
            "INVALID_REQUEST" => (
                StatusCodes.Status400BadRequest,
                result.Code,
                "The password change request is invalid."),
            _ => (
                result.StatusCode is StatusCodes.Status504GatewayTimeout
                    ? StatusCodes.Status504GatewayTimeout
                    : StatusCodes.Status502BadGateway,
                "IDENTITY_UNAVAILABLE",
                "The student identity service is temporarily unavailable."),
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
