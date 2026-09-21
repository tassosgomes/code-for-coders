using System.Text.Json;
using CodeForCoders.BffStudent.Application.Interfaces;

namespace CodeForCoders.BffStudent.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(BffStudentDbContext dbContext) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(message, payload));
        return Task.CompletedTask;
    }
}
