using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccount;

public sealed class ConfirmStudentAccount(
    IIdentityConfirmationStore confirmationStore,
    IStudentConfirmationMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<ConfirmStudentAccountInput> validator) : IConfirmStudentAccount
{
    public const string OperationId = "confirmStudentAccountInternal";
    private const string InvalidTokenCode = "INVALID_VERIFICATION_TOKEN";
    private const string InvalidTokenTitle = "The confirmation link is invalid or expired.";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<ConfirmStudentAccountOutput> ExecuteAsync(
        ConfirmStudentAccountInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var values = new ConfirmationValues(
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint(input.Token));
        var existingAttempt = await confirmationStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);

        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var token = await confirmationStore.FindVerificationTokenAsync(
            input.TenantId,
            HashToken(input.Token),
            cancellationToken);
        if (token is null
            || token.Purpose != ConfirmationPurpose
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

        var account = await confirmationStore.FindStudentAccountAsync(
            input.TenantId,
            token.AccountId,
            cancellationToken);
        if (account is null || account.DeactivatedOn is not null || account.IsConfirmed)
        {
            return await SaveRejectedAttemptAsync(
                existingAttempt,
                input.TenantId,
                values,
                now,
                cancellationToken);
        }

        token.Consume(now);
        account.Confirm();
        AddAttempt(existingAttempt, CreateAttempt(existingAttempt, input.TenantId, values, 204, null, null, now));
        await messageWriter.AppendAccountConfirmedAsync(account, now, cancellationToken);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new ConfirmStudentAccountOutput(204);
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

    private async Task<ConfirmStudentAccountOutput> SaveRejectedAttemptAsync(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        ConfirmationValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        AddAttempt(
            existingAttempt,
            CreateAttempt(existingAttempt, tenantId, values, 422, InvalidTokenCode, InvalidTokenTitle, now));
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(tenantId, values, now, cancellationToken);
        }

        throw new StudentConfirmationException(InvalidTokenCode, InvalidTokenTitle, InvalidTokenTitle);
    }

    private async Task<ConfirmStudentAccountOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        ConfirmationValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await confirmationStore.FindIdempotencyAsync(
            tenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (winner is not null && !winner.IsExpired(now))
        {
            return ResolveReplay(winner, values.Fingerprint);
        }

        throw InvalidToken();
    }

    private static IdempotencyRecord CreateAttempt(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        ConfirmationValues values,
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
            confirmationStore.AddIdempotencyRecord(attempt);
        }
    }

    private static ConfirmStudentAccountOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw new StudentConfirmationException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == 204)
        {
            return new ConfirmStudentAccountOutput(record.StatusCode);
        }

        throw new StudentConfirmationException(
            record.Code ?? InvalidTokenCode,
            record.Title ?? InvalidTokenTitle,
            record.Title ?? InvalidTokenTitle);
    }

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static StudentConfirmationException InvalidToken()
        => new(InvalidTokenCode, InvalidTokenTitle, InvalidTokenTitle);

    private sealed record ConfirmationValues(string KeyHash, string Fingerprint);

    private const string ConfirmationPurpose = "confirmacao-de-conta";
}
