using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(BffAdminDbContext dbContext) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(message, payload));
        return Task.CompletedTask;
    }
}
