using System.Collections.Concurrent;
using CodeForCoders.Commerce.Contracts;

namespace CodeForCoders.Commerce.Infra.Messaging;

public sealed class HeartbeatReceiptStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<CommercePlatformHeartbeatV1>> receipts = new();

    public Task<CommercePlatformHeartbeatV1> Register(Guid eventId)
    {
        var receipt = new TaskCompletionSource<CommercePlatformHeartbeatV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        receipts[eventId] = receipt;
        return receipt.Task;
    }

    public void MarkConsumed(CommercePlatformHeartbeatV1 heartbeat)
    {
        if (receipts.TryRemove(heartbeat.EventId, out var receipt))
        {
            receipt.TrySetResult(heartbeat);
        }
    }
}
