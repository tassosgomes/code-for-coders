using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class StudentRegistrationIdentityClientStub : IStudentRegistrationIdentityClient
{
    public StudentRegistrationResult Result { get; set; } = new(StatusCodes.Status202Accepted, null);

    public StudentRegistrationRequestV1? Request { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public StudentConfirmationResult ConfirmationResult { get; set; } = new(StatusCodes.Status204NoContent, null);

    public StudentConfirmationResult ConfirmationRequestResult { get; set; } = new(StatusCodes.Status202Accepted, null);

    public StudentAccountConfirmationTokenV1? ConfirmationRequest { get; private set; }

    public StudentAccountConfirmationEmailV1? ConfirmationEmailRequest { get; private set; }

    public Task<StudentRegistrationResult> RegisterAsync(
        StudentRegistrationRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Request = request;
        IdempotencyKey = idempotencyKey;
        return Task.FromResult(Result);
    }

    public Task<StudentConfirmationResult> ConfirmAsync(
        StudentAccountConfirmationTokenV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ConfirmationRequest = request;
        IdempotencyKey = idempotencyKey;
        return Task.FromResult(ConfirmationResult);
    }

    public Task<StudentConfirmationResult> RequestConfirmationAsync(
        StudentAccountConfirmationEmailV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ConfirmationEmailRequest = request;
        IdempotencyKey = idempotencyKey;
        return Task.FromResult(ConfirmationRequestResult);
    }
}
