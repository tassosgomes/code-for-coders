using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Sales.Common;
using CodeForCoders.Commerce.Domain.Entities;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.CreateOrder;

public sealed class CreateOrder(IOrderStore store, IPurchaseOfferReader catalog, IUnitOfWork unitOfWork,
    IOutboxMessageWriter outbox, TimeProvider clock) : ICreateOrder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<CreateOrderResult> ExecuteAsync(CreateOrderInput input, CancellationToken cancellationToken)
    {
        try { return await CreateAsync(input, cancellationToken); }
        catch (PendingOrderConflictException)
        {
            store.DiscardChanges();
            return await CreateAsync(input, cancellationToken);
        }
    }
    private async Task<CreateOrderResult> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken)
    {
        var scope = new OrderScope(input.TenantId, input.StudentId, Hash(input.IdempotencyKey));
        var requestHash = Hash(JsonSerializer.Serialize(new { input.OfferId }, JsonOptions));
        await using var transaction = await store.LockAsync(scope, cancellationToken);
        var receipt = await store.FindReceiptAsync(scope, cancellationToken);
        if (receipt is not null && receipt.ExpiresAt > clock.GetUtcNow())
        {
            if (receipt.RequestHash != requestHash) throw new OrderRuleException("IDEMPOTENCY_KEY_REUSED", "The idempotency key was used with a different request.");
            return new(JsonSerializer.Deserialize<StudentOrder>(receipt.ResponseJson, JsonOptions)!, receipt.StatusCode);
        }
        var offer = await catalog.FindAsync(input.OfferId, cancellationToken) ?? throw new NotFoundException("OFFER_NOT_AVAILABLE");
        var pending = await store.FindPendingAsync(input.StudentId, input.OfferId, cancellationToken);
        var number = pending is null ? await store.NextNumberAsync(input.TenantId, cancellationToken) : 0;
        var now = clock.GetUtcNow();
        var order = pending ?? Order.Create(input.TenantId, input.StudentId, new(number,
            new(offer.CourseId, offer.Title, offer.OfferId, offer.Name, offer.PriceCents, offer.AccessPeriod.Type, offer.AccessPeriod.Months), now));
        if (pending is null)
        {
            store.Add(order);
            await AppendFactAsync(order, input.TraceParent, cancellationToken);
        }
        var output = StudentOrder.FromOrder(order);
        var status = pending is null ? 201 : 200;
        receipt ??= OrderReceipt.Create(input.TenantId, input.StudentId, scope.KeyHash);
        receipt.Store(requestHash, new(JsonSerializer.Serialize(output, JsonOptions), status), now);
        store.Add(receipt);
        await unitOfWork.CommitAsync(cancellationToken);
        await transaction.CompleteAsync(cancellationToken);
        return new(output, status);
    }
    private Task AppendFactAsync(Order order, string? traceParent, CancellationToken cancellationToken)
    {
        var eventId = Guid.CreateVersion7();
        var payload = new
        {
            eventId,
            order.TenantId,
            orderId = order.Id,
            orderNumber = order.Number,
            order.StudentId,
            order.CourseId,
            order.OfferId,
            order.PriceCents,
            order.Currency,
            accessPeriod = new CodeForCoders.Commerce.Domain.ValueObjects.AccessPeriod(order.PeriodType, order.PeriodMonths),
            occurredAt = order.CreatedAt
        };
        return outbox.AppendAsync(new(eventId, order.TenantId, "PedidoCriado", "vendas.pedido-criado.v1", payload, order.CreatedAt, traceParent), cancellationToken);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
