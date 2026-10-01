using System.Text.Json;
using CodeForCoders.Commerce.Application.Interfaces;

namespace CodeForCoders.Commerce.Infra.Data.Outbox;

public sealed class CatalogOutboxMessageWriter(CommerceDbContext dbContext) : ICatalogOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        dbContext.CatalogOutboxMessages.Add(CatalogOutboxMessage.Create(message, payload));
        return Task.CompletedTask;
    }
}
