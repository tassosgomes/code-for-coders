using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
namespace CodeForCoders.Commerce.Infra.Data.Sales;

public sealed class SalesAccessSink(IOrderPaymentStore store, ITenantContext tenant, IUnitOfWork unitOfWork) : ISalesAccessSink
{
    public async Task ApplyAsync(PurchaseAccessFact fact, CancellationToken cancellationToken)
    {
        if (fact.Origin != "purchase") return;
        if (fact.TenantId == Guid.Empty || fact.OriginRef is not { } orderId || orderId == Guid.Empty || fact.GrantId == Guid.Empty)
            throw new OrderRuleException("ACCESS_INVALID", "Access fact is invalid.");
        tenant.Set(fact.TenantId);
        await using var transaction = await store.LockAsync(fact.TenantId, orderId, cancellationToken);
        var order = await store.FindAsync(orderId, cancellationToken)
         ?? throw new OrderRuleException("ORDER_UNKNOWN", "Access refers to an unknown order.");
        if (order.StudentId != fact.StudentId || order.CourseId != fact.CourseId)
            throw new OrderRuleException("ACCESS_INVALID", "Access does not match the purchased course.");
        order.RecordAccess(fact.GrantId, fact.GrantedAt);
        await unitOfWork.CommitAsync(cancellationToken); await transaction.CompleteAsync(cancellationToken);
    }
}
