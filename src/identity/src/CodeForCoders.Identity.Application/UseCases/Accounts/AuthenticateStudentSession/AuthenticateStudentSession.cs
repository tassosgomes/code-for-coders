using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStudentSession;

public sealed class AuthenticateStudentSession(
    IIdentitySessionStore sessionStore,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<StudentSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    IValidator<AuthenticateStudentSessionInput> validator) : IAuthenticateStudentSession
{
    public const string OperationId = "createStudentSessionInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);
    private static readonly string DummyPasswordHash =
        $"pbkdf2-sha256$310000${Convert.ToBase64String(new byte[16])}${Convert.ToBase64String(new byte[32])}";

    public async Task<AuthenticateStudentSessionOutput> ExecuteAsync(
        AuthenticateStudentSessionInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        var keyHash = fingerprinter.HashKey(input.IdempotencyKey);
        var fingerprint = fingerprinter.Fingerprint(string.Empty, normalizedEmail, input.Password);
        var existing = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            keyHash,
            cancellationToken);
        if (existing is not null && !existing.IsExpired(now))
        {
            return await ResolveReplayAsync(existing, fingerprint, now, cancellationToken);
        }

        var (account, credential) = await sessionStore.FindStudentCredentialAsync(
            input.TenantId,
            normalizedEmail,
            cancellationToken);
        var passwordIsValid = passwordHasher.Verify(input.Password, credential?.PasswordHash ?? DummyPasswordHash);
        if (account is null || credential is null || account.Type != AccountType.Student
            || account.DeactivatedOn is not null || !passwordIsValid)
        {
            return await SaveRejectedAttemptAsync(
                existing,
                input.TenantId,
                keyHash,
                fingerprint,
                StatusCodesUnauthorized,
                "INVALID_CREDENTIALS",
                "Student credentials are invalid.",
                now,
                cancellationToken);
        }

        if (!account.IsConfirmed)
        {
            return await SaveRejectedAttemptAsync(
                existing,
                input.TenantId,
                keyHash,
                fingerprint,
                StatusCodesUnprocessableEntity,
                "EMAIL_NOT_CONFIRMED",
                "The student account email address has not been confirmed.",
                now,
                cancellationToken);
        }

        var expiresOn = now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes);
        var session = StudentSession.Create(
            Guid.CreateVersion7(now),
            input.TenantId,
            account.Id,
            now,
            expiresOn);
        var attempt = CreateAttempt(
            existing,
            input.TenantId,
            keyHash,
            fingerprint,
            StatusCodesOk,
            null,
            null,
            now);
        attempt.SetStudentSessionId(session.Id);
        sessionStore.AddSession(session);
        if (existing is null)
        {
            sessionStore.AddIdempotencyRecord(attempt);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException)
        {
            var winner = await sessionStore.FindIdempotencyAsync(
                input.TenantId,
                OperationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ResolveReplayAsync(winner, fingerprint, now, cancellationToken);
            }

            throw;
        }

        return ToOutput(session.Id, account.Id, account.Name, expiresOn);
    }

    private async Task<AuthenticateStudentSessionOutput> ResolveReplayAsync(
        IdempotencyRecord record,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.Fingerprint),
                Convert.FromHexString(fingerprint)))
        {
            throw Failure(
                StatusCodesUnprocessableEntity,
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == StatusCodesOk && record.StudentSessionId is Guid sessionId)
        {
            var active = await sessionStore.RenewActiveSessionAsync(
                record.TenantId,
                sessionId,
                now,
                now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes),
                cancellationToken);
            return active is null
                ? throw InvalidCredentials()
                : ToOutput(active.SessionId, active.AccountId, active.Name, active.ExpiresAt);
        }

        throw Failure(
            record.StatusCode,
            record.Code ?? "INVALID_CREDENTIALS",
            record.Title ?? "Student credentials are invalid.");
    }

    private async Task<AuthenticateStudentSessionOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existing,
        Guid tenantId,
        string keyHash,
        string fingerprint,
        int statusCode,
        string code,
        string title,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var attempt = CreateAttempt(existing, tenantId, keyHash, fingerprint, statusCode, code, title, now);
        if (existing is null)
        {
            sessionStore.AddIdempotencyRecord(attempt);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException)
        {
            var winner = await sessionStore.FindIdempotencyAsync(
                tenantId,
                OperationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ResolveReplayAsync(winner, fingerprint, now, cancellationToken);
            }

            throw;
        }

        throw Failure(statusCode, code, title);
    }

    private static IdempotencyRecord CreateAttempt(
        IdempotencyRecord? existing,
        Guid tenantId,
        string keyHash,
        string fingerprint,
        int statusCode,
        string? code,
        string? title,
        DateTimeOffset now)
    {
        if (existing is not null)
        {
            existing.Refresh(fingerprint, statusCode, code, title, now, now.Add(IdempotencyWindow));
            return existing;
        }

        return IdempotencyRecord.Create(
            tenantId,
            OperationId,
            keyHash,
            fingerprint,
            statusCode,
            code,
            title,
            now,
            now.Add(IdempotencyWindow));
    }

    private static AuthenticateStudentSessionOutput ToOutput(
        Guid sessionId,
        Guid accountId,
        string name,
        DateTimeOffset expiresAt)
        => new(sessionId, accountId, name, expiresAt);

    private static StudentSessionException InvalidCredentials()
        => Failure(StatusCodesUnauthorized, "INVALID_CREDENTIALS", "Student credentials are invalid.");

    private static StudentSessionException Failure(int statusCode, string code, string title)
        => new(statusCode, code, title, title);

    private const int StatusCodesOk = 200;
    private const int StatusCodesUnauthorized = 401;
    private const int StatusCodesUnprocessableEntity = 422;
}
