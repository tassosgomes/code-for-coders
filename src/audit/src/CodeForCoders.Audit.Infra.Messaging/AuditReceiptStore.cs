using System.Collections.Concurrent;
using CodeForCoders.Audit.Contracts;

namespace CodeForCoders.Audit.Infra.Messaging;

public sealed class AuditReceiptStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AuditEventV1>> receipts = new();

    public Task<AuditEventV1> Register(Guid eventId)
    {
        var receipt = new TaskCompletionSource<AuditEventV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        receipts[eventId] = receipt;
        return receipt.Task;
    }

    public void MarkConsumed(AuditEventV1 auditEvent)
    {
        if (receipts.TryRemove(auditEvent.EventId, out var receipt))
        {
            receipt.TrySetResult(auditEvent);
        }
    }
}
