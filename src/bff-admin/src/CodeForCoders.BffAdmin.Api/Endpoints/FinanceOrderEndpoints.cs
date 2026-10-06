using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Api.Security;
namespace CodeForCoders.BffAdmin.Api.Endpoints;

public static class FinanceOrderEndpoints
{
    public static void MapFinanceOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/orders").WithTags("FinanceOrders");
        group.MapGet("/", ListAsync).WithName("listFinanceOrders");
        group.MapGet("/{orderId:guid}", GetAsync).WithName("getFinanceOrder");
    }
    private static async Task<IResult> ListAsync([AsParameters] FinanceOrdersRequest filters, HttpContext context,
        IStaffSessionIdentityClient sessions, ICommerceFinanceAreaClient commerce, IStudentAccountIdentityClient students, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(context, sessions, cancellationToken);
        if (authorization.Error is not null) return authorization.Error;
        var result = await commerce.ListOrdersAsync(authorization.Token!, new(filters.Status, filters.CourseId, filters.StudentId,
            filters.CreatedFrom, filters.CreatedTo, filters.Page ?? 1, filters.Size ?? 20), cancellationToken);
        if (result.StatusCode != 200 || result.Value is null) return Problem(result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE");
        var ids = result.Value.Data.Select(order => order.StudentId).Distinct().ToArray();
        if (ids.Length == 0) return Results.Ok(new FinanceOrderPage([], result.Value.Pagination));
        var resolution = await students.ResolveAsync(BffSessionContext.Get(context)!.IdentitySessionId, ids, cancellationToken);
        if (resolution.StatusCode != 200 || resolution.Data is null) return Problem(resolution.StatusCode, resolution.Code ?? "IDENTITY_UNAVAILABLE");
        var accounts = resolution.Data.ToDictionary(account => account.StudentId);
        if (ids.Any(id => !accounts.ContainsKey(id))) return Problem(502, "IDENTITY_UNAVAILABLE");
        return Results.Ok(new FinanceOrderPage(result.Value.Data.Select(row => FinanceOrderSummary.FromCommerce(row,
            new(row.StudentId, accounts[row.StudentId].Name, accounts[row.StudentId].Email))).ToArray(), result.Value.Pagination));
    }
    private static async Task<IResult> GetAsync(Guid orderId, HttpContext context, IStaffSessionIdentityClient sessions,
        ICommerceFinanceAreaClient commerce, IStudentAccountIdentityClient students, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(context, sessions, cancellationToken);
        if (authorization.Error is not null) return authorization.Error;
        var result = await commerce.GetOrderAsync(authorization.Token!, orderId, cancellationToken);
        if (result.StatusCode != 200 || result.Value is null) return Problem(result.StatusCode, result.Code ?? "COMMERCE_UNAVAILABLE");
        var resolution = await students.ResolveAsync(BffSessionContext.Get(context)!.IdentitySessionId, [result.Value.StudentId], cancellationToken);
        if (resolution.StatusCode != 200 || resolution.Data is null) return Problem(resolution.StatusCode, resolution.Code ?? "IDENTITY_UNAVAILABLE");
        var account = resolution.Data.SingleOrDefault(student => student.StudentId == result.Value.StudentId);
        if (account is null) return Problem(502, "IDENTITY_UNAVAILABLE");
        return Results.Ok(FinanceOrderDetail.FromCommerce(result.Value, new(account.StudentId, account.Name, account.Email)));
    }
    private static async Task<(string? Token, IResult? Error)> AuthorizeAsync(HttpContext context, IStaffSessionIdentityClient identity, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var session = BffSessionContext.Get(context);
        var current = BffSessionContext.GetValidatedSession(context);
        if (session is null || current is null) return (null, Problem(401, "SESSION_REQUIRED"));
        if (!current.Permissions.Contains("financeiro.ler", StringComparer.Ordinal)) return (null, Problem(403, "PERMISSION_DENIED"));
        var validation = await identity.ValidateSessionAsync(session.IdentitySessionId, "commerce", cancellationToken);
        if (validation.StatusCode == 401 && validation.Code == "SESSION_REQUIRED") return (null, Problem(401, "SESSION_REQUIRED"));
        if (validation.StatusCode != 200 || validation.Session is null || string.IsNullOrWhiteSpace(validation.Session.AccessToken))
            return (null, Problem(502, "IDENTITY_UNAVAILABLE"));
        if (!validation.Session.Permissions.Contains("financeiro.ler", StringComparer.Ordinal)) return (null, Problem(403, "PERMISSION_DENIED"));
        return (validation.Session.AccessToken, null);
    }
    private static IResult Problem(int status, string code)
        => Results.Problem(statusCode: status, title: "Finance order request rejected.", extensions: new Dictionary<string, object?> { ["code"] = code });
}
