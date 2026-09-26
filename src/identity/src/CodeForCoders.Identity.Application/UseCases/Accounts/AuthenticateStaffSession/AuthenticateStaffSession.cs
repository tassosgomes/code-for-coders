using System.Security.Cryptography;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;

public sealed class AuthenticateStaffSession(
    IIdentitySessionStore sessionStore,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<StaffSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    IValidator<AuthenticateStaffSessionInput> validator) : IAuthenticateStaffSession
{
    public const string OperationId = "createStaffSessionInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);
    private static readonly string DummyPasswordHash =
        $"pbkdf2-sha256$310000${Convert.ToBase64String(new byte[16])}${Convert.ToBase64String(new byte[32])}";

    public async Task<AuthenticateStaffSessionOutput> ExecuteAsync(
        AuthenticateStaffSessionInput input,
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

        var (account, credential) = await sessionStore.FindInternalCredentialAsync(
            input.TenantId,
            normalizedEmail,
            cancellationToken);
        var passwordIsValid = passwordHasher.Verify(input.Password, credential?.PasswordHash ?? DummyPasswordHash);
        if (account is null || credential is null || !passwordIsValid)
        {
            return await SaveRejectedAttemptAsync(
                existing,
                input.TenantId,
                keyHash,
                fingerprint,
                UnauthorizedStatusCode,
                "INVALID_CREDENTIALS",
                "Internal actor credentials are invalid.",
                now,
                cancellationToken);
        }

        var expiresAt = now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes);
        var session = StaffSession.Create(
            Guid.CreateVersion7(now),
            input.TenantId,
            account.Id,
            now,
            expiresAt);
        var attempt = CreateAttempt(
            existing,
            input.TenantId,
            keyHash,
            fingerprint,
            OkStatusCode,
            null,
            null,
            now);
        attempt.SetStaffSessionId(session.Id);
        sessionStore.AddStaffSession(session);
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

        var roles = await sessionStore.GetStaffRolesAsync(input.TenantId, account.Id, cancellationToken);
        return ToOutput(session.Id, account.Id, account.Name, roles, expiresAt);
    }

    private async Task<AuthenticateStaffSessionOutput> ResolveReplayAsync(
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
                UnprocessableEntityStatusCode,
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == OkStatusCode && record.StaffSessionId is Guid sessionId)
        {
            var active = await sessionStore.RenewActiveStaffSessionAsync(
                record.TenantId,
                sessionId,
                now,
                now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes),
                cancellationToken);
            return active is null
                ? throw InvalidCredentials()
                : ToOutput(active);
        }

        throw Failure(
            record.StatusCode,
            record.Code ?? "INVALID_CREDENTIALS",
            record.Title ?? "Internal actor credentials are invalid.");
    }

    private async Task<AuthenticateStaffSessionOutput> SaveRejectedAttemptAsync(
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
            var winner = await sessionStore.FindIdempotencyAsync(tenantId, OperationId, keyHash, cancellationToken);
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

    private static AuthenticateStaffSessionOutput ToOutput(
        Guid sessionId,
        Guid accountId,
        string name,
        IReadOnlyList<string> roles,
        DateTimeOffset expiresAt)
    {
        var permissions = roles
            .SelectMany(StaffRoleCatalog.GetPermissions)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AuthenticateStaffSessionOutput(sessionId, accountId, name, roles, permissions, expiresAt);
    }

    private static AuthenticateStaffSessionOutput ToOutput(StaffSessionDetails session)
        => new(session.SessionId, session.AccountId, session.Name, session.Roles, session.Permissions, session.ExpiresAt);

    private static StaffSessionException InvalidCredentials()
        => Failure(UnauthorizedStatusCode, "INVALID_CREDENTIALS", "Internal actor credentials are invalid.");

    private static StaffSessionException Failure(int statusCode, string code, string title)
        => new(statusCode, code, title, title);

    private const int OkStatusCode = 200;
    private const int UnauthorizedStatusCode = 401;
    private const int UnprocessableEntityStatusCode = 422;
}
