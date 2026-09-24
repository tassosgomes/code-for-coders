using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentPasswordRecoveryIdentityClient
{
    Task<StudentPasswordRecoveryResult> RequestPasswordResetAsync(
        StudentPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StudentPasswordRecoveryResult> ResetPasswordAsync(
        StudentPasswordResetV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
