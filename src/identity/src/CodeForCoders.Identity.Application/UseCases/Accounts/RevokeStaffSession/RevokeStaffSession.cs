using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffSession;

public sealed class RevokeStaffSession(
    IIdentitySessionStore sessionStore,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<RevokeStaffSessionInput> validator) : IRevokeStaffSession
{
    public const string OperationId = "revokeStaffSessionInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<bool> ExecuteAsync(RevokeStaffSessionInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var keyHash = fingerprinter.HashKey(input.IdempotencyKey);
        var fingerprint = fingerprinter.Fingerprint(input.SessionId.ToString("D"));
        var existing = await sessionStore.FindIdempotencyAsync(input.TenantId, OperationId, keyHash, cancellationToken);
        if (existing is not null && !existing.IsExpired(now))
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existing.Fingerprint),
                    Convert.FromHexString(fingerprint)))
            {
                throw Failure(UnprocessableEntityStatusCode, "IDEMPOTENCY_CONFLICT");
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
            record.Refresh(fingerprint, NoContentStatusCode, null, null, now, now.Add(IdempotencyWindow));
        }

        await sessionStore.RevokeStaffSessionAsync(input.TenantId, input.SessionId, now, cancellationToken);
        if (existing is null)
        {
            sessionStore.AddIdempotencyRecord(record);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch (RegistrationWriteConflictException)
        {
            var winner = await sessionStore.FindIdempotencyAsync(input.TenantId, OperationId, keyHash, cancellationToken);
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

    private static StaffSessionException Failure(int statusCode, string code)
    {
        const string title = "The idempotency key was already used with a different request.";
        return new StaffSessionException(statusCode, code, title, title);
    }
}
