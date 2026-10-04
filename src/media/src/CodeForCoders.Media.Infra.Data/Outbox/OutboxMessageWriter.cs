using System.Text.Json;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(MediaDbContext dbContext, IOptions<OutboxOptions> options) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HashSet<string> _retainedKeys = new(options.Value.RetainedRoutingKeys, StringComparer.OrdinalIgnoreCase);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        var entity = OutboxMessage.Create(message, payload);
        if (_retainedKeys.Contains(message.RoutingKey))
        {
            entity.MarkProcessed();
        }

        dbContext.OutboxMessages.Add(entity);
        return Task.CompletedTask;
    }
}

