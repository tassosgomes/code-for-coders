using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStudentPassword;

public sealed class ResetStudentPassword(
    IIdentityPasswordRecoveryStore recoveryStore,
    IStudentPasswordRecoveryMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<ResetStudentPasswordInput> validator) : IResetStudentPassword
{
    public const string OperationId = "resetStudentPasswordInternal";
    private const string RejectedCode = "PASSWORD_RESET_REJECTED";
    private const string RejectedTitle = "The password reset link or new password is invalid.";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<ResetStudentPasswordOutput> ExecuteAsync(
        ResetStudentPasswordInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var values = new ResetValues(
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint(PasswordRecoveryPurpose, input.Token, input.NewPassword));
        var existingAttempt = await recoveryStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var token = await recoveryStore.FindVerificationTokenAsync(
            input.TenantId,
            HashToken(input.Token),
            cancellationToken);
        if (token is null
            || token.Purpose != PasswordRecoveryPurpose
            || token.ExpiresOn <= now
            || token.ConsumedOn is not null)
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                now,
                cancellationToken);
        }

        var account = await recoveryStore.FindStudentAccountAsync(
            input.TenantId,
            token.AccountId,
            cancellationToken);
        var credential = account is null
            ? null
            : await recoveryStore.FindCredentialAsync(input.TenantId, account.Id, cancellationToken);
        if (account is null || credential is null)
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                now,
                cancellationToken);
        }

        if (!StudentPasswordPolicy.IsSatisfiedBy(input.NewPassword))
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                now,
                cancellationToken);
        }

        var recoveryTokens = await recoveryStore.FindUnconsumedVerificationTokensAsync(
            input.TenantId,
            account.Id,
            PasswordRecoveryPurpose,
            now,
            cancellationToken);
        foreach (var recoveryToken in recoveryTokens)
        {
            recoveryToken.Consume(now);
        }

        credential.ReplacePasswordHash(passwordHasher.Hash(input.NewPassword));
        var sessions = await recoveryStore.FindUnrevokedStudentSessionsAsync(
            input.TenantId,
            account.Id,
            cancellationToken);
        foreach (var session in sessions)
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
            return new ResetStudentPasswordOutput(204);
        }
        catch (ConcurrentWriteException)
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
        }
    }

    private async Task<ResetStudentPasswordOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        ResetValues values,
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
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(tenantId, values, now, cancellationToken);
        }

        throw Rejected();
    }

    private async Task<ResetStudentPasswordOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        ResetValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await recoveryStore.FindIdempotencyAsync(
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
        ResetValues values,
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
            recoveryStore.AddIdempotencyRecord(attempt);
        }
    }

    private static ResetStudentPasswordOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw new StudentPasswordRecoveryException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == 204)
        {
            return new ResetStudentPasswordOutput(204);
        }

        throw new StudentPasswordRecoveryException(
            record.Code ?? RejectedCode,
            record.Title ?? RejectedTitle,
            record.Title ?? RejectedTitle);
    }

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static StudentPasswordRecoveryException Rejected()
        => new(RejectedCode, RejectedTitle, RejectedTitle);

    private sealed record ResetValues(string KeyHash, string Fingerprint);

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
