using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RequestStudentAccountConfirmation;

public sealed class RequestStudentAccountConfirmation(
    IIdentityConfirmationStore confirmationStore,
    IStudentConfirmationMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<RegistrationOptions> registrationOptions,
    TimeProvider timeProvider,
    IValidator<RequestStudentAccountConfirmationInput> validator) : IRequestStudentAccountConfirmation
{
    public const string OperationId = "requestAccountConfirmationInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<RequestStudentAccountConfirmationOutput> ExecuteAsync(
        RequestStudentAccountConfirmationInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        var values = new RequestValues(
            normalizedEmail,
            fingerprinter.HashKey(input.IdempotencyKey),
            fingerprinter.Fingerprint(normalizedEmail));
        var existingAttempt = await confirmationStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);

        if (existingAttempt is not null && !existingAttempt.IsExpired(now))
        {
            return ResolveReplay(existingAttempt, values.Fingerprint);
        }

        var account = await confirmationStore.FindEligibleStudentByEmailAsync(
            input.TenantId,
            values.NormalizedEmail,
            cancellationToken);
        if (account is not null)
        {
            confirmationStore.RequireStillUnconfirmed(account);
            var rawToken = CreateToken();
            confirmationStore.AddVerificationToken(VerificationToken.Create(
                Guid.CreateVersion7(now),
                input.TenantId,
                account.Id,
                ConfirmationPurpose,
                HashToken(rawToken),
                now.AddHours(registrationOptions.Value.ConfirmationLifetimeHours)));
            await messageWriter.AppendConfirmationRequestedAsync(account, rawToken, now, cancellationToken);
        }

        AddAttempt(
            existingAttempt,
            CreateAttempt(existingAttempt, input.TenantId, values, now));
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return new RequestStudentAccountConfirmationOutput(202);
        }
        catch (ConcurrentWriteException)
        {
            var winner = await FindWinnerAsync(input.TenantId, values, now, cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            var neutralAttempt = CreateAttempt(null, input.TenantId, values, now);
            confirmationStore.AddIdempotencyRecord(neutralAttempt);
            try
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return new RequestStudentAccountConfirmationOutput(202);
            }
            catch (RegistrationWriteConflictException exception)
                when (exception.ConstraintName == "ux_idempotency_records_scope_key")
            {
                return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
            }
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            return await ResolveConcurrentCommitAsync(input.TenantId, values, now, cancellationToken);
        }
    }

    private async Task<RequestStudentAccountConfirmationOutput?> FindWinnerAsync(
        Guid tenantId,
        RequestValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await confirmationStore.FindIdempotencyAsync(
            tenantId,
            OperationId,
            values.KeyHash,
            cancellationToken);
        return winner is not null && !winner.IsExpired(now)
            ? ResolveReplay(winner, values.Fingerprint)
            : null;
    }

    private async Task<RequestStudentAccountConfirmationOutput> ResolveConcurrentCommitAsync(
        Guid tenantId,
        RequestValues values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winner = await FindWinnerAsync(tenantId, values, now, cancellationToken);
        return winner ?? throw new InvalidOperationException("The confirmation request conflicted without an idempotency record.");
    }

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
            confirmationStore.AddIdempotencyRecord(attempt);
        }
    }

    private static RequestStudentAccountConfirmationOutput ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw new StudentConfirmationException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                "The idempotency key was already used with a different request.");
        }

        return new RequestStudentAccountConfirmationOutput(record.StatusCode);
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
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record RequestValues(string NormalizedEmail, string KeyHash, string Fingerprint);

    private const string ConfirmationPurpose = "confirmacao-de-conta";
}
