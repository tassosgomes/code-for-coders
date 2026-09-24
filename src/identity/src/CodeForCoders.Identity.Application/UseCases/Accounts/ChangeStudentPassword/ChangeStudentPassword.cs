using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStudentPassword;

public sealed class ChangeStudentPassword(
    IIdentityPasswordRecoveryStore passwordStore,
    IStudentPasswordRecoveryMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<ChangeStudentPasswordInput> validator) : IChangeStudentPassword
{
    public const string OperationId = "changeStudentPasswordInternal";
    private const int UnauthorizedStatusCode = 401;
    private const string RejectedCode = "PASSWORD_CHANGE_REJECTED";
    private const string RejectedTitle = "The current password or new password is invalid.";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<ChangeStudentPasswordOutput> ExecuteAsync(
        ChangeStudentPasswordInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var account = await FindActiveAccountAsync(input.TenantId, input.SessionId, now, cancellationToken);
        var values = new ChangeValues(
            fingerprinter.HashKey($"{account.Id:D}:{input.IdempotencyKey}"),
            fingerprinter.Fingerprint(input.SessionId.ToString("D"), input.CurrentPassword, input.NewPassword));
        var existingAttempt = await passwordStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var credential = await passwordStore.FindCredentialAsync(input.TenantId, account.Id, cancellationToken);
        if (credential is null
            || !passwordHasher.Verify(input.CurrentPassword, credential.PasswordHash)
            || !StudentPasswordPolicy.IsSatisfiedBy(input.NewPassword))
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                input.SessionId,
                values,
                now,
                cancellationToken);
        }

        var recoveryTokens = await passwordStore.FindUnconsumedVerificationTokensAsync(
            input.TenantId,
            account.Id,
            PasswordRecoveryPurpose,
            now,
            cancellationToken);
        foreach (var token in recoveryTokens)
        {
            token.Consume(now);
        }

        credential.ReplacePasswordHash(passwordHasher.Hash(input.NewPassword));
        var sessions = await passwordStore.FindUnrevokedStudentSessionsAsync(
            input.TenantId,
            account.Id,
            cancellationToken);
        foreach (var session in sessions.Where(session => session.Id != input.SessionId))
        {
            session.Revoke(now);
        }

        AddAttempt(
            existingAttempt,
            CreateAttempt(existingAttempt, input.TenantId, values, 204, null, null, now));
        await messageWriter.AppendPasswordResetAsync(account, now, cancellationToken);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new ChangeStudentPasswordOutput(204);
        }
        catch (ConcurrentWriteException)
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, input.SessionId, values, now, cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, input.SessionId, values, now, cancellationToken);
        }
    }

    private async Task<Account> FindActiveAccountAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await passwordStore.FindActiveStudentSessionAsync(
            tenantId,
            sessionId,
            now,
            cancellationToken);
        if (session is null)
        {
            throw SessionRequired();
        }

        var account = await passwordStore.FindStudentAccountAsync(tenantId, session.AccountId, cancellationToken);
        if (account is null || !account.IsConfirmed)
        {
            throw SessionRequired();
        }

        return account;
    }

    private async Task<ChangeStudentPasswordOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        Guid sessionId,
        ChangeValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        AddAttempt(
            existingAttempt,
            CreateAttempt(existingAttempt, tenantId, values, 422, RejectedCode, RejectedTitle, now));
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (ConcurrentWriteException)
        {
            return await ResolveConcurrentCommitAsync(tenantId, sessionId, values, now, cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(tenantId, sessionId, values, now, cancellationToken);
        }

        throw Rejected();
    }

    private async Task<ChangeStudentPasswordOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        Guid sessionId,
        ChangeValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await FindActiveAccountAsync(tenantId, sessionId, now, cancellationToken);

        var winner = await passwordStore.FindIdempotencyAsync(
            tenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (winner is not null && !winner.IsExpired(now))
        {
            return ResolveReplay(winner, values.Fingerprint);
        }

        throw Rejected();
    }

    private static IdempotencyRecord CreateAttempt(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        ChangeValues values,
        int statusCode,
        string? code,
        string? title,
        DateTimeOffset now)
    {
        if (existingAttempt is not null)
        {
            existingAttempt.Refresh(
                values.Fingerprint,
                statusCode,
                code,
                title,
                now,
                now.Add(IdempotencyWindow));
            return existingAttempt;
        }

        return IdempotencyRecord.Create(
            tenantId,
            OperationId,
            values.KeyHash,
            values.Fingerprint,
            statusCode,
            code,
            title,
            now,
            now.Add(IdempotencyWindow));
    }

    private void AddAttempt(IdempotencyRecord? existingAttempt, IdempotencyRecord attempt)
    {
        if (existingAttempt is null)
        {
            passwordStore.AddIdempotencyRecord(attempt);
        }
    }

    private static ChangeStudentPasswordOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw new StudentPasswordChangeException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == 204)
        {
            return new ChangeStudentPasswordOutput(204);
        }

        throw new StudentPasswordChangeException(
            record.Code ?? RejectedCode,
            record.Title ?? RejectedTitle,
            record.Title ?? RejectedTitle);
    }

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private static StudentPasswordChangeException Rejected()
        => new(RejectedCode, RejectedTitle, RejectedTitle);

    private static StudentSessionException SessionRequired()
        => new(
            UnauthorizedStatusCode,
            "SESSION_REQUIRED",
            "The student session is missing or no longer active.",
            "The student session is missing or no longer active.");

    private sealed record ChangeValues(string KeyHash, string Fingerprint);

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
