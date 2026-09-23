using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Infra.Data.Outbox;

public sealed class OutboxMessageWriter(
    IdentityDbContext dbContext,
    IOptions<OutboxDestinationOptions> destinationOptions) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message.Payload, JsonOptions);
        var exchange = message.Exchange ?? destinationOptions.Value.Exchange;
        dbContext.OutboxMessages.Add(OutboxMessage.Create(message with { Exchange = exchange }, payload));
        return Task.CompletedTask;
    }
}
