using System.Collections.Concurrent;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class InMemoryBffSessionStore : IBffSessionStore
{
    private readonly ConcurrentDictionary<string, OpaqueBffSession> sessions = new(StringComparer.Ordinal);

    public Task StoreAsync(string opaqueCookieValue, OpaqueBffSession session, CancellationToken cancellationToken)
    {
        sessions[opaqueCookieValue] = session;
        return Task.CompletedTask;
    }

    public Task<OpaqueBffSession?> GetAsync(string sessionId, CancellationToken cancellationToken)
        => Task.FromResult(sessions.GetValueOrDefault(sessionId));

    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken)
    {
        sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public OpaqueBffSession? GetStored(string opaqueCookieValue)
        => sessions.GetValueOrDefault(opaqueCookieValue);
}
