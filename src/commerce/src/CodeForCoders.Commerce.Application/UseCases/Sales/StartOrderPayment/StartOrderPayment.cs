using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Options;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.StartOrderPayment;

public sealed class StartOrderPayment(IOrderPaymentStore store, IBillingPaymentClient billing, IUnitOfWork unitOfWork,
 ITenantContext tenant, IOptions<StudentAppOptions> app) : IStartOrderPayment
{
    public async Task<BillingPaymentSession> ExecuteAsync(StartOrderPaymentInput input, CancellationToken cancellationToken)
    {
        await using var transaction = await store.LockAsync(tenant.TenantId!.Value, input.OrderId, cancellationToken);
        var order = await store.FindAsync(input.OrderId, cancellationToken);
        if (order is null || order.StudentId != input.StudentId) throw new NotFoundException("ORDER_NOT_FOUND");
        order.EnsurePayable();
        var returnUrl = $"{app.Value.PublicBaseUrl.TrimEnd('/')}/student/pedidos/{order.Id:D}";
        var session = await billing.EnsureAsync(new(order.TenantId, order.Id, order.StudentId, order.PriceCents, order.Currency,
         $"{order.CourseTitle} — {order.OfferName}", $"{returnUrl}?resultado=concluido", $"{returnUrl}?resultado=saiu"), cancellationToken);
        order.OpenPayment(session.ExpiresAt);
        await unitOfWork.CommitAsync(cancellationToken); await transaction.CompleteAsync(cancellationToken); return session;
    }
}
