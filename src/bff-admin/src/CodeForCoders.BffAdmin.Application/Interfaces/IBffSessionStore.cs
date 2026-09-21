using CodeForCoders.BffAdmin.Application.Common;

namespace CodeForCoders.BffAdmin.Application.Interfaces;

public interface IBffSessionStore
{
    Task StoreAsync(OpaqueBffSession session, CancellationToken cancellationToken);

    Task<OpaqueBffSession?> GetAsync(string sessionId, CancellationToken cancellationToken);

    Task RemoveAsync(string sessionId, CancellationToken cancellationToken);
}
