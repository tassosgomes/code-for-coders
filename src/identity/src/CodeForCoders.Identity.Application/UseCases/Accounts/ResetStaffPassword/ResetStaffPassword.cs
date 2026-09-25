using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResetStaffPassword;

public sealed class ResetStaffPassword(
    IIdentityPasswordRecoveryStore passwordRecoveryStore,
    IIdentityStaffAccountStore staffAccountStore,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<ResetStaffPasswordInput> validator) : IResetStaffPassword
{
    public const string OperationId = "resetStaffPasswordInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<ResetStaffPasswordOutput> ExecuteAsync(
        ResetStaffPasswordInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var values = new ResetValues(
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint("staff-password-reset", input.Token, input.NewPassword));
        var existingAttempt = await passwordRecoveryStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var token = await passwordRecoveryStore.FindVerificationTokenAsync(
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
                "RESET_TOKEN_INVALID",
                "This password reset link is invalid.",
                now,
                cancellationToken);
        }

        var account = await staffAccountStore.FindInternalAccountAsync(
            input.TenantId,
            token.AccountId,
            cancellationToken);
        if (account is null)
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                "RESET_TOKEN_INVALID",
                "This password reset link is invalid.",
                now,
                cancellationToken);
        }

        if (!StudentPasswordPolicy.IsSatisfiedBy(input.NewPassword))
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                "PASSWORD_POLICY_VIOLATION",
                "The new password does not satisfy the password policy.",
                now,
                cancellationToken);
        }

        var recoveryTokens = await passwordRecoveryStore.FindUnconsumedVerificationTokensAsync(
            input.TenantId,
            account.Id,
            PasswordRecoveryPurpose,
            now,
            cancellationToken);
        foreach (var recoveryToken in recoveryTokens)
        {
            recoveryToken.Consume(now);
        }

        var credential = await passwordRecoveryStore.FindCredentialAsync(
            input.TenantId,
            account.Id,
            cancellationToken);
        if (credential is null)
        {
            passwordRecoveryStore.AddCredential(Credential.Create(
                Guid.CreateVersion7(now.AddTicks(3)),
                input.TenantId,
                account.Id,
                passwordHasher.Hash(input.NewPassword),
                now));
        }
        else
        {
            credential.ReplacePasswordHash(passwordHasher.Hash(input.NewPassword));
        }

        var sessions = await staffAccountStore.FindUnrevokedStaffSessionsAsync(
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
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new ResetStaffPasswordOutput(204);
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

    private async Task<ResetStaffPasswordOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        ResetValues values,
        string code,
        string title,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        AddAttempt(existingAttempt, CreateAttempt(existingAttempt, tenantId, values, 422, code, title, now));
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(tenantId, values, now, cancellationToken);
        }

        throw Rejected(code, title);
    }

    private async Task<ResetStaffPasswordOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        ResetValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await passwordRecoveryStore.FindIdempotencyAsync(
            tenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (winner is not null && !winner.IsExpired(now))
        {
            return ResolveReplay(winner, values.Fingerprint);
        }

        throw Rejected("RESET_TOKEN_INVALID", "This password reset link is invalid.");
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
            passwordRecoveryStore.AddIdempotencyRecord(attempt);
        }
    }

    private static ResetStaffPasswordOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw Rejected(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == 204)
        {
            return new ResetStaffPasswordOutput(204);
        }

        throw Rejected(record.Code ?? "RESET_TOKEN_INVALID", record.Title ?? "This password reset link is invalid.");
    }

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static StaffPasswordRecoveryException Rejected(string code, string title)
        => new(code, title, title);

    private sealed record ResetValues(string KeyHash, string Fingerprint);

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
