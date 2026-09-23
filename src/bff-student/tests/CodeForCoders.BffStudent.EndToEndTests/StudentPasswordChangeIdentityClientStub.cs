using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class StudentPasswordChangeIdentityClientStub : IStudentPasswordChangeIdentityClient
{
    public StudentPasswordChangeResult Result { get; set; } = new(StatusCodes.Status204NoContent, null);

    public Guid SessionId { get; private set; }

    public StudentPasswordChangeV1? Request { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public int Calls { get; private set; }

    public Task<StudentPasswordChangeResult> ChangePasswordAsync(
        Guid sessionId,
        StudentPasswordChangeV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        SessionId = sessionId;
        Request = request;
        IdempotencyKey = idempotencyKey;
        Calls++;
        return Task.FromResult(Result);
    }
}
