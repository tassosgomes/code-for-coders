using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(
    BffAdminDbContext dbContext,
    OutboxPayloadProtector payloadProtector) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, message.Payload.GetType(), JsonOptions);
        var keyVersion = message.ProtectPayload ? payloadProtector.KeyVersion : null;
        if (message.ProtectPayload)
        {
            payload = payloadProtector.Protect(message.Id, message.TenantId, payload);
        }

        dbContext.OutboxMessages.Add(OutboxMessage.Create(message, payload, keyVersion));
        return Task.CompletedTask;
    }
}
