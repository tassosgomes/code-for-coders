using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(CommerceDbContext dbContext) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(message, payload));
        return Task.CompletedTask;
    }
}
