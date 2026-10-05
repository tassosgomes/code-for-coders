using System.Text.Json;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;

namespace CodeForCoders.Billing.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(
    BillingDbContext dbContext,
    ITenantContext tenantContext) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        var processingNamespace = message.Namespace
            ?? tenantContext.Namespace;
        dbContext.OutboxMessages.Add(OutboxMessage.Create(message, payload, processingNamespace));
        return Task.CompletedTask;
    }
}
