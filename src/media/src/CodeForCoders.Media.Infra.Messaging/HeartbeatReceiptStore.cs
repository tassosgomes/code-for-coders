using System.Collections.Concurrent;
using CodeForCoders.Media.Contracts;

namespace CodeForCoders.Media.Infra.Messaging;

public sealed class HeartbeatReceiptStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<PlatformHeartbeatV1>> receipts = new();

    public Task<PlatformHeartbeatV1> Register(Guid eventId)
    {
        var receipt = new TaskCompletionSource<PlatformHeartbeatV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        receipts[eventId] = receipt;
        return receipt.Task;
    }

    public void MarkConsumed(PlatformHeartbeatV1 heartbeat)
    {
        if (receipts.TryRemove(heartbeat.EventId, out var receipt))
        {
            receipt.TrySetResult(heartbeat);
        }
    }
}
