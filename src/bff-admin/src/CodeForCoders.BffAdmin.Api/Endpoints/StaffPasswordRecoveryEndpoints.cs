using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class StaffPasswordRecoveryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffPasswordRecoveryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/staff-password-reset-requests", RequestStaffPasswordResetAsync)
            .WithName("RequestStaffPasswordReset")
            .WithTags("StaffCredential")
            .Accepts<StaffPasswordRecoveryRequestV1>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> RequestStaffPasswordResetAsync(
        HttpContext httpContext,
        IStaffPasswordResetIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StaffPasswordRecoveryRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffPasswordRecoveryRequestV1>(
                httpContext.Request.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "A valid JSON request is required.");
        }

        var email = request?.Email;
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(email)
            || email.Length > 254
            || !new EmailAddressAttribute().IsValid(email)
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128)
        {
            return Problem(httpContext, StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Email and Idempotency-Key must be valid.");
        }

        var result = await identityClient.RequestPasswordResetAsync(
            new StaffPasswordRecoveryRequestV1(email),
            idempotencyKey,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status202Accepted)
        {
            return Results.StatusCode(StatusCodes.Status202Accepted);
        }

        var code = result.Code == "IDEMPOTENCY_CONFLICT" ? "IDEMPOTENCY_KEY_REUSED" : result.Code;
        return Problem(
            httpContext,
            result.StatusCode,
            code ?? "IDENTITY_UNAVAILABLE",
            code == "IDENTITY_UNAVAILABLE" ? "Identity is unavailable." : "Password reset request was rejected.");
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
