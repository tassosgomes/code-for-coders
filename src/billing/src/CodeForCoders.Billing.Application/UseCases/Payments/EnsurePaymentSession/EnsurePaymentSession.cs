using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using FluentValidation;
namespace CodeForCoders.Billing.Application.UseCases.Payments.EnsurePaymentSession;

public sealed class EnsurePaymentSession(IPaymentStore store, IPaymentGateway gateway, IPaymentReturnPolicy policy,
 ITenantContext tenant, IUnitOfWork unitOfWork, IValidator<EnsurePaymentSessionInput> validator, TimeProvider clock) : IEnsurePaymentSession
{
    public async Task<PaymentSessionOutput> ExecuteAsync(EnsurePaymentSessionInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (!policy.Allows(input.SuccessUrl) || !policy.Allows(input.CancelUrl))
            throw new ValidationException("Return URL is not allowed.");
        var tenantId = tenant.TenantId ?? throw new PaymentRuleException("INVALID_REQUEST", "School is required.");
        await using var transaction = await store.LockAsync(tenantId, input.OrderId, cancellationToken);
        var terms = new PaymentTerms(input.StudentId, input.AmountCents, input.Currency, input.Description);
        var payment = await store.FindAsync(input.OrderId, cancellationToken);
        GatewaySession session;
        if (payment is null)
        {
            session = await gateway.OpenAsync(new(tenantId, input.OrderId, terms, input.SuccessUrl, input.CancelUrl), cancellationToken);
            payment = Payment.Create(tenantId, input.OrderId, terms);
            payment.Open(session.Reference, session.ExpiresAt, clock.GetUtcNow()); store.Add(payment);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        else
        {
            payment.EnsureTerms(terms); payment.EnsurePayable(clock.GetUtcNow());
            session = await gateway.GetAsync(payment.SessionReference, cancellationToken);
        }
        await transaction.CompleteAsync(cancellationToken);
        return new(input.OrderId, "checkout", session.PaymentUrl, null, session.ExpiresAt);
    }
}
