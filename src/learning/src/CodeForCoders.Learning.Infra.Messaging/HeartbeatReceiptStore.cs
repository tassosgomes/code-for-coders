using System.Collections.Concurrent;
using CodeForCoders.Learning.Contracts;

namespace CodeForCoders.Learning.Infra.Messaging;

public sealed class HeartbeatReceiptStore
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<LearningPlatformHeartbeatV1>> receipts = new();

    public Task<LearningPlatformHeartbeatV1> Register(Guid eventId)
    {
        var receipt = new TaskCompletionSource<LearningPlatformHeartbeatV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        receipts[eventId] = receipt;
        return receipt.Task;
    }

    public void MarkConsumed(LearningPlatformHeartbeatV1 heartbeat)
    {
        if (receipts.TryRemove(heartbeat.EventId, out var receipt))
        {
            receipt.TrySetResult(heartbeat);
        }
    }
}
