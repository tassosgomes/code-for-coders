using CodeForCoders.BffStudent.Application.Common;

namespace CodeForCoders.BffStudent.Application.Interfaces;

public interface IBffSessionStore
{
    Task StoreAsync(string opaqueCookieValue, OpaqueBffSession session, CancellationToken cancellationToken);

    Task<OpaqueBffSession?> GetAsync(string sessionId, CancellationToken cancellationToken);

    Task RemoveAsync(string sessionId, CancellationToken cancellationToken);
}
