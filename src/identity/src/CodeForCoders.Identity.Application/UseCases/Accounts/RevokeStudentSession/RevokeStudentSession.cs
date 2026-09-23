using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStudentSession;

public sealed class RevokeStudentSession(
    IIdentitySessionStore sessionStore,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<RevokeStudentSessionInput> validator) : IRevokeStudentSession
{
    public const string OperationId = "revokeStudentSessionInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<bool> ExecuteAsync(RevokeStudentSessionInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var keyHash = fingerprinter.HashKey(input.IdempotencyKey);
        var fingerprint = fingerprinter.Fingerprint(input.SessionId.ToString("D"));
        var existing = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            keyHash,
            cancellationToken);
        if (existing is not null && !existing.IsExpired(now))
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existing.Fingerprint),
                    Convert.FromHexString(fingerprint)))
            {
                throw new StudentSessionException(
                    UnprocessableEntityStatusCode,
                    "IDEMPOTENCY_CONFLICT",
                    "The idempotency key was already used with a different request.",
                    "The idempotency key was already used with a different request.");
            }

            return true;
        }

        var record = existing ?? IdempotencyRecord.Create(
            input.TenantId,
            OperationId,
            keyHash,
            fingerprint,
            NoContentStatusCode,
            null,
            null,
            now,
            now.Add(IdempotencyWindow));
        if (existing is not null)
        {
            record.Refresh(
                fingerprint,
                NoContentStatusCode,
                null,
                null,
                now,
                now.Add(IdempotencyWindow));
        }

        await sessionStore.RevokeSessionAsync(input.TenantId, input.SessionId, now, cancellationToken);
        if (existing is null)
        {
            sessionStore.AddIdempotencyRecord(record);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch (CodeForCoders.Identity.Domain.Exceptions.RegistrationWriteConflictException)
        {
            var winner = await sessionStore.FindIdempotencyAsync(
                input.TenantId,
                OperationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now)
                && CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(winner.Fingerprint),
                    Convert.FromHexString(fingerprint)))
            {
                return true;
            }

            throw;
        }
    }

    private const int NoContentStatusCode = 204;
    private const int UnprocessableEntityStatusCode = 422;
}
