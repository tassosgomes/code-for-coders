using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentPasswordChangeIdentityClient
{
    Task<StudentPasswordChangeResult> ChangePasswordAsync(
        Guid sessionId,
        StudentPasswordChangeV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
