using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public sealed class EntitlementOutboxMessageWriter(CommerceDbContext dbContext) : IEntitlementOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        dbContext.EntitlementOutboxMessages.Add(EntitlementOutboxMessage.Create(message, payload));
        return Task.CompletedTask;
    }
}
