using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class StudentRegistrationIdentityClientStub : IStudentRegistrationIdentityClient
{
    public StudentRegistrationResult Result { get; set; } = new(StatusCodes.Status202Accepted, null);

    public StudentRegistrationRequestV1? Request { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Request = request;
        IdempotencyKey = idempotencyKey;
        return Task.FromResult(Result);
    }
}
