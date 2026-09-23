using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IStudentRegistrationIdentityClient
{
    Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
