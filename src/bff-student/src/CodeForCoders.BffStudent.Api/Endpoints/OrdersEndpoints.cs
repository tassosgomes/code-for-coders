using CodeForCoders.BffStudent.Api.ApiModels;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
namespace CodeForCoders.BffStudent.Api.Endpoints;

public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/offers/{offerId:guid}/purchase-summary", SummaryAsync).WithName("getPurchaseSummary");
        endpoints.MapPost("/api/v1/orders", CreateAsync).WithName("createOrder");
        endpoints.MapGet("/api/v1/orders/{orderId:guid}", GetAsync).WithName("getMyOrder");
    }
    private static async Task<IResult> SummaryAsync(Guid offerId, HttpContext context, IOrdersCommerceClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        return Result(await client.SendAsync(new(HttpMethod.Get, $"internal/v1/offers/{offerId:D}/purchase-summary",
            BffSessionContext.GetAccessToken(context)!), cancellationToken));
    }
    private static async Task<IResult> GetAsync(Guid orderId, HttpContext context, IOrdersCommerceClient client, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        return Result(await client.SendAsync(new(HttpMethod.Get, $"internal/v1/orders/{orderId:D}",
            BffSessionContext.GetAccessToken(context)!), cancellationToken));
    }
    private static async Task<IResult> CreateAsync(CreateOrderRequest body, HttpContext context, IOrdersCommerceClient client, CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (body.OfferId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 128) return Result(new(400, "VALIDATION_ERROR"));
        var result = await client.SendAsync(new(HttpMethod.Post, "internal/v1/orders", BffSessionContext.GetAccessToken(context)!, body.OfferId, key), cancellationToken);
        if (result.StatusCode == 201) context.Response.Headers.Location = $"/api/v1/orders/{result.Body!.Value.GetProperty("orderId").GetGuid():D}";
        return Result(result);
    }
    private static IResult Result(OrderProxyResult result) => result.StatusCode is 200 or 201
        ? Results.Json(result.Body, statusCode: result.StatusCode)
        : Results.Problem(statusCode: result.StatusCode, title: "Não foi possível processar a compra agora.",
            extensions: new Dictionary<string, object?> { ["code"] = result.Code });
}
