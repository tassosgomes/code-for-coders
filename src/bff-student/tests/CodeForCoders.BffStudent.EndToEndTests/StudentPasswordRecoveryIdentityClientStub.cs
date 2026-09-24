using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class StudentPasswordRecoveryIdentityClientStub : IStudentPasswordRecoveryIdentityClient
{
    public StudentPasswordRecoveryResult RequestResult { get; set; } = new(StatusCodes.Status202Accepted, null);

    public StudentPasswordRecoveryResult ResetResult { get; set; } = new(StatusCodes.Status204NoContent, null);

    public StudentPasswordResetRequestV1? Request { get; private set; }

    public StudentPasswordResetV1? Reset { get; private set; }

    public string? RequestIdempotencyKey { get; private set; }

    public string? ResetIdempotencyKey { get; private set; }

    public int RequestCalls { get; private set; }

    public Task<StudentPasswordRecoveryResult> RequestPasswordResetAsync(
        StudentPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Request = request;
        RequestIdempotencyKey = idempotencyKey;
        RequestCalls++;
        return Task.FromResult(RequestResult);
    }

    public Task<StudentPasswordRecoveryResult> ResetPasswordAsync(
        StudentPasswordResetV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Reset = request;
        ResetIdempotencyKey = idempotencyKey;
        return Task.FromResult(ResetResult);
    }
}
