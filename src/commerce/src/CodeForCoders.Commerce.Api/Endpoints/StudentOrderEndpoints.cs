using CodeForCoders.Commerce.Application.UseCases.Sales.StartOrderPayment;
using CodeForCoders.Commerce.Api.ApiModels;
using CodeForCoders.Commerce.Api.Authorization;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.Sales.CreateOrder;
using CodeForCoders.Commerce.Application.UseCases.Sales.GetPurchaseSummary;
using CodeForCoders.Commerce.Application.UseCases.Sales.GetStudentOrder;
using CodeForCoders.Commerce.Application.UseCases.Sales.CancelOrder;
using CodeForCoders.Commerce.Application.UseCases.Sales.ListStudentOrders;
namespace CodeForCoders.Commerce.Api.Endpoints;

public static class StudentOrderEndpoints
{
    public static void MapStudentOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/internal/v1").RequireAuthorization(StudentOrderPolicies.Use).WithTags("Orders");
        group.MapGet("/offers/{offerId:guid}/purchase-summary", SummaryAsync).WithName("getPurchaseSummaryInternal");
        group.MapPost("/orders", CreateAsync).WithName("createOrderInternal");
        group.MapGet("/orders", ListAsync).WithName("listStudentOrdersInternal");
        group.MapPost("/orders/{orderId:guid}/payment-session", PaymentAsync).WithName("startOrderPaymentInternal");
        group.MapPost("/orders/{orderId:guid}/cancellation", CancelAsync).WithName("cancelOrderInternal");
        group.MapGet("/orders/{orderId:guid}", GetAsync).WithName("getStudentOrderInternal");
    }
    private static async Task<IResult> ListAsync(HttpContext context, ITenantContext tenant,
        IListStudentOrders useCase, CancellationToken cancellationToken, int _page = 1, int _size = 10)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(student, _page, _size), cancellationToken));
    }
    private static async Task<IResult> SummaryAsync(Guid offerId, HttpContext context, ITenantContext tenant,
        IGetPurchaseSummary useCase, CancellationToken cancellationToken)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(student, offerId), cancellationToken));
    }
    private static async Task<IResult> GetAsync(Guid orderId, HttpContext context, ITenantContext tenant,
        IGetStudentOrder useCase, CancellationToken cancellationToken)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(student, orderId), cancellationToken));
    }
    private static async Task<IResult> CreateAsync(CreateOrderRequest body, HttpContext context, ITenantContext tenant,
        ICreateOrder useCase, CancellationToken cancellationToken)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (body.OfferId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 128) return Problem(400, "INVALID_REQUEST");
        var result = await useCase.ExecuteAsync(new(tenant.TenantId!.Value, student, body.OfferId, key,
            System.Diagnostics.Activity.Current?.Id), cancellationToken);
        return result.StatusCode == 201 ? Results.Created($"/internal/v1/orders/{result.Order.OrderId:D}", result.Order) : Results.Ok(result.Order);
    }
    private static async Task<IResult> PaymentAsync(Guid orderId, HttpContext context, ITenantContext tenant,
        IStartOrderPayment useCase, CancellationToken cancellationToken)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        context.Response.Headers.CacheControl = "no-store";
        var session = await useCase.ExecuteAsync(new(student, orderId), cancellationToken);
        return Results.Ok(new
        {
            orderId = session.OrderId,
            kind = session.Kind,
            paymentUrl = session.PaymentUrl,
            expiresAt = session.ExpiresAt
        });
    }
    private static async Task<IResult> CancelAsync(Guid orderId, HttpContext context, ITenantContext tenant,
        ICancelOrder useCase, CancellationToken cancellationToken)
    {
        if (!SetBuyer(context, tenant, out var student)) return Problem(401, "TOKEN_INVALID");
        context.Response.Headers.CacheControl = "private, no-store";
        var result = await useCase.ExecuteAsync(new(orderId, student, System.Diagnostics.Activity.Current?.Id), cancellationToken);
        return Results.Ok(result);
    }
    private static bool SetBuyer(HttpContext context, ITenantContext tenant, out Guid student)
    {
        student = Guid.Empty;
        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var school) || school == Guid.Empty
            || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out student) || student == Guid.Empty) return false;
        tenant.Set(school); return true;
    }
    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status, title: "Order request rejected.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
