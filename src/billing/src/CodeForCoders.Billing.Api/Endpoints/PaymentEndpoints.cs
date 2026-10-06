using CodeForCoders.Billing.Api.ApiModels;
using CodeForCoders.Billing.Api.Security;
using CodeForCoders.Billing.Application.UseCases.Payments.EnsurePaymentSession;
using CodeForCoders.Billing.Application.UseCases.Payments.ReceiveGatewayEvent;
namespace CodeForCoders.Billing.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/internal/v1/payment-sessions/{orderId:guid}", EnsureAsync).WithName("ensurePaymentSessionInternal");
        endpoints.MapPost("/webhooks/v1/stripe/events", ReceiveAsync).WithName("receiveGatewayEvent");
    }
    private static async Task<IResult> EnsureAsync(Guid orderId, PaymentSessionRequest input, HttpContext context,
     ServiceAssertionVerifier verifier, IEnsurePaymentSession useCase, CodeForCoders.Billing.Application.Common.ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : null;
        var verified = await verifier.VerifyDetailedAsync(token, "payment:request", cancellationToken);
        if (verified.Assertion is null) return Problem(401, "SERVICE_ASSERTION_INVALID");
        if (!verified.ScopeGranted) return Problem(403, "SCOPE_DENIED");
        tenantContext.Set(verified.Assertion.TenantId);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await useCase.ExecuteAsync(new(orderId, input.StudentId, input.AmountCents, input.Currency,
         input.Description, input.SuccessUrl, input.CancelUrl), cancellationToken));
    }
    private static async Task<IResult> ReceiveAsync(HttpContext context, IReceiveGatewayEvent useCase, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        await useCase.ExecuteAsync(new(body, context.Request.Headers["Stripe-Signature"].ToString()), cancellationToken);
        return Results.Ok(new { received = true });
    }
    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status, title: "Service assertion rejected.",
     extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "" });
}
