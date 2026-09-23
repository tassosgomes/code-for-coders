using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentSessionIdentityClient
{
    Task<StudentSessionCreatedResult> CreateSessionAsync(
        StudentSessionLoginV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StudentSessionValidatedResult> ValidateSessionAsync(
        Guid sessionId,
        string? audience,
        CancellationToken cancellationToken);

    Task<StudentSessionRevokedResult> RevokeSessionAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
