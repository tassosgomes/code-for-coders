using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;

namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class FinanceAreaEndpoints
{
    private const string FinancePermission = "financeiro.ler";

    public static void MapFinanceAreaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/finance-area", GetFinanceAreaAsync)
            .WithName("GetFinanceArea")
            .WithTags("FinanceArea")
            .Produces<FinanceAreaResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetFinanceAreaAsync(
        HttpContext httpContext,
        IStaffSessionIdentityClient identityClient,
        ICommerceFinanceAreaClient commerceClient,
        CancellationToken cancellationToken)
    {
        var session = BffSessionContext.Get(httpContext);
        var currentValidation = BffSessionContext.GetValidatedSession(httpContext);
        if (session is null || currentValidation is null)
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        if (!currentValidation.Permissions.Contains(FinancePermission, StringComparer.Ordinal))
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "Você não tem permissão para esta área.");
        }

        var validation = await identityClient.ValidateSessionAsync(
            session.IdentitySessionId,
            "commerce",
            cancellationToken);
        if (validation.StatusCode == StatusCodes.Status401Unauthorized && validation.Code == "SESSION_REQUIRED")
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "SESSION_REQUIRED", "A current staff session is required.");
        }

        if (validation.StatusCode != StatusCodes.Status200OK
            || validation.Session is null
            || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
        {
            var status = validation.StatusCode == StatusCodes.Status504GatewayTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            return Problem(httpContext, status, "IDENTITY_UNAVAILABLE", "The staff identity service is temporarily unavailable.");
        }

        var commerce = await commerceClient.GetFinanceAreaAsync(validation.Session.AccessToken, cancellationToken);
        if (commerce.StatusCode == StatusCodes.Status200OK && commerce.Area is not null)
        {
            return Results.Ok(commerce.Area);
        }

        if (commerce.StatusCode == StatusCodes.Status401Unauthorized && commerce.Code == "TOKEN_INVALID")
        {
            return Problem(httpContext, StatusCodes.Status401Unauthorized, "TOKEN_INVALID", "Token de acesso inválido.");
        }

        if (commerce.StatusCode == StatusCodes.Status403Forbidden && commerce.Code == "PERMISSION_DENIED")
        {
            return Problem(httpContext, StatusCodes.Status403Forbidden, "PERMISSION_DENIED", "Você não tem permissão para esta área.");
        }

        throw new InvalidOperationException("Commerce returned an unexpected finance area response.");
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
