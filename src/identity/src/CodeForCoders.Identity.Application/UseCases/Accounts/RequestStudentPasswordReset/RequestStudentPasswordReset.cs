using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentPasswordReset;

public sealed class RequestStudentPasswordReset(
    IIdentityPasswordRecoveryStore recoveryStore,
    IStudentPasswordRecoveryMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<RegistrationOptions> registrationOptions,
    TimeProvider timeProvider,
    IValidator<RequestStudentPasswordResetInput> validator) : IRequestStudentPasswordReset
{
    public const string OperationId = "requestStudentPasswordResetInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<RequestStudentPasswordResetOutput> ExecuteAsync(
        RequestStudentPasswordResetInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        var values = new RequestValues(
            normalizedEmail,
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint(normalizedEmail));
        var existingAttempt = await recoveryStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var account = await recoveryStore.FindEligibleStudentByEmailAsync(
            input.TenantId,
            values.NormalizedEmail,
            cancellationToken);
        if (account is not null)
        {
            var rawToken = CreateToken();
            recoveryStore.AddVerificationToken(VerificationToken.Create(
                Guid.CreateVersion7(now),
                input.TenantId,
                account.Id,
                PasswordRecoveryPurpose,
                HashToken(rawToken),
                now.AddHours(registrationOptions.Value.PasswordResetLifetimeHours)));
            await messageWriter.AppendPasswordResetRequestedAsync(account, rawToken, now, cancellationToken);
        }

        AddAttempt(existingAttempt, CreateAttempt(existingAttempt, input.TenantId, values, now));
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new RequestStudentPasswordResetOutput(202);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
        }
        catch (ConcurrentWriteException)
        {
            var winner = await FindWinnerAsync(input.TenantId, values, now, cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            recoveryStore.AddIdempotencyRecord(CreateAttempt(null, input.TenantId, values, now));
            try
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return new RequestStudentPasswordResetOutput(202);
            }
            catch (RegistrationWriteConflictException exception)
                when (exception.ConstraintName == "ux_idempotency_records_scope_key")
            {
                return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
            }
        }
    }

    private async Task<RequestStudentPasswordResetOutput?> FindWinnerAsync(
        Guid tenantId,
        RequestValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await recoveryStore.FindIdempotencyAsync(
            tenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        return winner is not null && !winner.IsExpired(now)
            ? ResolveReplay(winner, values.Fingerprint)
            : null;
    }

    private async Task<RequestStudentPasswordResetOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        RequestValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await FindWinnerAsync(tenantId, values, now, cancellationToken)
            ?? throw new InvalidOperationException("The password reset request conflicted without an idempotency record.");

    private static IdempotencyRecord CreateAttempt(
        IdempotencyRecord? existingAttempt,
        Guid tenantId,
        RequestValues values,
        DateTimeOffset now)
    {
        if (existingAttempt is not null)
        {
            existingAttempt.Refresh(
                values.Fingerprint,
                202,
                null,
                null,
                now,
                now.Add(IdempotencyWindow));
            return existingAttempt;
        }

        return IdempotencyRecord.Create(
            tenantId,
            OperationId,
            values.KeyHash,
            values.Fingerprint,
            202,
            null,
            null,
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

    private static RequestStudentPasswordResetOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw IdempotencyConflict();
        }

        return new RequestStudentPasswordResetOutput(record.StatusCode);
    }

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static StudentPasswordRecoveryException IdempotencyConflict()
        => new(
            "IDEMPOTENCY_CONFLICT",
            "The idempotency key was already used with a different request.",
            "The idempotency key was already used with a different request.");

    private sealed record RequestValues(string NormalizedEmail, string KeyHash, string Fingerprint);

    private const string PasswordRecoveryPurpose = "recuperacao-de-senha";
}
