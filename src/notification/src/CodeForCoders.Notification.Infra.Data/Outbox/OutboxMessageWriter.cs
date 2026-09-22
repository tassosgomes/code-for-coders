using System.Text.Json;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;

namespace CodeForCoders.Notification.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(
    NotificationDbContext dbContext,
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
