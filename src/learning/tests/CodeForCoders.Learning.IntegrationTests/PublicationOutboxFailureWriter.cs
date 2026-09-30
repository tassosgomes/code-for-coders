using CodeForCoders.Learning.Application.Interfaces;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class PublicationOutboxFailureWriter(IOutboxMessageWriter inner, Func<bool> fail) : IOutboxMessageWriter
{
    public Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        if (fail() && message.RoutingKey == "auditoria.ato-praticado.v1") throw new IOException("Injected failure between publication outbox rows.");
        return inner.AppendAsync(message, cancellationToken);
    }
}
