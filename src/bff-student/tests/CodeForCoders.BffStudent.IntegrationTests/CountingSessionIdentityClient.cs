using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.IntegrationTests;

/// <summary>Identity must never be consulted by the anonymous showcase, whatever the cookie says.</summary>
public sealed class CountingSessionIdentityClient : IStudentSessionIdentityClient
{
    private int calls;

    public int Calls => Volatile.Read(ref calls);

    public Task<StudentSessionCreatedResult> CreateSessionAsync(StudentSessionLoginV1 request, string idempotencyKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        throw new InvalidOperationException("Identity must not be called by the showcase.");
    }

    public Task<StudentSessionValidatedResult> ValidateSessionAsync(Guid sessionId, string? audience, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        throw new InvalidOperationException("Identity must not be called by the showcase.");
    }

    public Task<StudentSessionRevokedResult> RevokeSessionAsync(Guid sessionId, string idempotencyKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        throw new InvalidOperationException("Identity must not be called by the showcase.");
    }
}
