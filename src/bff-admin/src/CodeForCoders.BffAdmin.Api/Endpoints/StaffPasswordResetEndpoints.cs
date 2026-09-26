using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class StaffPasswordResetEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapStaffPasswordResetEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/staff-password-resets", ResetStaffPasswordAsync)
            .WithName("ResetStaffPassword")
            .WithTags("StaffCredential")
            .Accepts<StaffPasswordResetRequestV1>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    private static async Task<IResult> ResetStaffPasswordAsync(
        HttpContext httpContext,
        IStaffPasswordResetIdentityClient identityClient,
        CancellationToken cancellationToken)
    {
        StaffPasswordResetRequestV1? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<StaffPasswordResetRequestV1>(
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

        var code = result.Code == "IDEMPOTENCY_CONFLICT" ? "IDEMPOTENCY_KEY_REUSED" : result.Code;
        return Problem(
            httpContext,
            result.StatusCode,
            code ?? "IDENTITY_UNAVAILABLE",
            code == "IDENTITY_UNAVAILABLE" ? "Identity is unavailable." : "Password reset was rejected.");
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
