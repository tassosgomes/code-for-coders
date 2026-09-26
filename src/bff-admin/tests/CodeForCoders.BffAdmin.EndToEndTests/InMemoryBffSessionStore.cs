using System.Collections.Concurrent;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class InMemoryBffSessionStore : IBffSessionStore
{
    private readonly ConcurrentDictionary<string, OpaqueBffSession> sessions = new(StringComparer.Ordinal);

    public int StoreCount { get; private set; }

    public int RemoveCount { get; private set; }

    public Task StoreAsync(OpaqueBffSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions[session.SessionId] = session;
        StoreCount++;
        return Task.CompletedTask;
    }

    public Task<OpaqueBffSession?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.TryRemove(sessionId, out _);
        RemoveCount++;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        sessions.Clear();
        StoreCount = 0;
        RemoveCount = 0;
    }
}
