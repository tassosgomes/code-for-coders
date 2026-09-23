using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentRegistrationIdentityClient
{
    Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StudentConfirmationResult> ConfirmAsync(
        StudentAccountConfirmationTokenV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StudentConfirmationResult> RequestConfirmationAsync(
        StudentAccountConfirmationEmailV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
