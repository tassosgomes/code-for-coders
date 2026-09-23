using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class StudentSessionIdentityClientStub : IStudentSessionIdentityClient
{
    public StudentSessionCreatedResult CreateResult { get; set; } = new(
        200,
        null,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "Ana Souza",
        DateTimeOffset.MaxValue);

    public StudentSessionValidatedResult ValidateResult { get; set; } = new(
        200,
        null,
        Guid.CreateVersion7(),
        "Ana Souza",
        DateTimeOffset.MaxValue);

    public StudentSessionRevokedResult RevokeResult { get; set; } = new(204, null);

    public StudentSessionLoginV1? LoginRequest { get; private set; }

    public string? LoginIdempotencyKey { get; private set; }

    public Guid ValidatedSessionId { get; private set; }

    public string? ValidatedAudience { get; private set; }

    public int ValidationCalls { get; private set; }

    public Guid RevokedSessionId { get; private set; }

    public string? RevocationIdempotencyKey { get; private set; }

    public int RevocationCalls { get; private set; }

    public Task<StudentSessionCreatedResult> CreateSessionAsync(
        StudentSessionLoginV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        LoginRequest = request;
        LoginIdempotencyKey = idempotencyKey;
        return Task.FromResult(CreateResult);
    }

    public Task<StudentSessionValidatedResult> ValidateSessionAsync(
        Guid sessionId,
        string? audience,
        CancellationToken cancellationToken)
    {
        ValidatedSessionId = sessionId;
        ValidatedAudience = audience;
        ValidationCalls++;
        return Task.FromResult(ValidateResult);
    }

    public Task<StudentSessionRevokedResult> RevokeSessionAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RevokedSessionId = sessionId;
        RevocationIdempotencyKey = idempotencyKey;
        RevocationCalls++;
        return Task.FromResult(RevokeResult);
    }
}
