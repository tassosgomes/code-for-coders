using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.AcceptStaffInvitation;

public sealed class AcceptStaffInvitation(
    IIdentityStaffAccountStore staffAccountStore,
    IIdentityStaffInvitationStore invitationStore,
    IIdentitySessionStore sessionStore,
    IStaffInvitationMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    IPasswordHasher passwordHasher,
    IOptions<StaffSessionOptions> sessionOptions,
    TimeProvider timeProvider,
    IValidator<AcceptStaffInvitationInput> validator) : IAcceptStaffInvitation
{
    public const string OperationId = "acceptStaffInvitationInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public async Task<AcceptStaffInvitationOutput> ExecuteAsync(
        AcceptStaffInvitationInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var token = input.Token;
        var name = input.Name.Trim();
        var normalizedTokenHash = HashToken(token);
        var keyHash = fingerprinter.HashKey(input.IdempotencyKey);
        var fingerprint = fingerprinter.Fingerprint(JsonSerializer.Serialize(new
        {
            Token = token,
            Name = name,
            input.Password,
        }));

        var existingRecord = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            keyHash,
            cancellationToken);
        if (existingRecord is not null && !existingRecord.IsExpired(now))
        {
            return await ReplayAsync(existingRecord, fingerprint, now, cancellationToken);
        }

        var invitation = await invitationStore.FindByTokenHashAsync(
            input.TenantId,
            normalizedTokenHash,
            cancellationToken);
        if (!IsAcceptable(invitation, now))
        {
            throw InvalidInvitation();
        }

        if (!StudentPasswordPolicy.IsSatisfiedBy(input.Password))
        {
            throw new StaffInvitationException(
                422,
                "PASSWORD_POLICY_VIOLATION",
                "The password does not satisfy the password policy.");
        }

        var existingAccount = await staffAccountStore.FindActiveAccountByNormalizedEmailAsync(
            input.TenantId,
            invitation!.NormalizedEmail,
            cancellationToken);
        if (existingAccount is not null)
        {
            throw EmailUnavailable();
        }

        return await CreateAccountAsync(
            input,
            invitation,
            existingRecord,
            keyHash,
            fingerprint,
            normalizedTokenHash,
            name,
            now,
            cancellationToken);
    }

    private async Task<AcceptStaffInvitationOutput> CreateAccountAsync(
        AcceptStaffInvitationInput input,
        StaffInvitation invitation,
        IdempotencyRecord? existingRecord,
        string keyHash,
        string fingerprint,
        string tokenHash,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accountId = Guid.CreateVersion7(now);
        var account = Account.CreateInternal(
            accountId,
            input.TenantId,
            name,
            invitation.Email,
            invitation.NormalizedEmail);
        var credential = Credential.Create(
            Guid.CreateVersion7(now.AddTicks(1)),
            input.TenantId,
            accountId,
            passwordHasher.Hash(input.Password),
            now);
        var roleAssignment = StaffRoleAssignment.Create(
            Guid.CreateVersion7(now.AddTicks(2)),
            input.TenantId,
            accountId,
            invitation.OfferedRole,
            now);
        var session = StaffSession.Create(
            Guid.CreateVersion7(now.AddTicks(3)),
            input.TenantId,
            accountId,
            now,
            now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes));
        invitation.Accept(now);

        staffAccountStore.AddAccount(account);
        staffAccountStore.AddCredential(credential);
        staffAccountStore.AddRoleAssignment(roleAssignment);
        sessionStore.AddStaffSession(session);
        var record = existingRecord ?? IdempotencyRecord.Create(
            input.TenantId,
            OperationId,
            keyHash,
            fingerprint,
            StatusCodesLikeOk,
            null,
            null,
            now,
            now.Add(IdempotencyWindow));
        if (existingRecord is null)
        {
            sessionStore.AddIdempotencyRecord(record);
        }
        else
        {
            record.Refresh(fingerprint, StatusCodesLikeOk, null, null, now, now.Add(IdempotencyWindow));
        }

        record.SetStaffSessionId(session.Id);
        await messageWriter.AppendInvitationAcceptedAsync(invitation, accountId, now, cancellationToken);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_idempotency_records_scope_key")
        {
            var winner = await sessionStore.FindIdempotencyAsync(
                input.TenantId,
                OperationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ReplayAsync(winner, fingerprint, now, cancellationToken);
            }

            throw;
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_accounts_tenant_id_normalized_email")
        {
            throw await ResolveWriteConflictAsync(input.TenantId, tokenHash, now, cancellationToken);
        }
        catch (ConcurrentWriteException)
        {
            throw await ResolveWriteConflictAsync(input.TenantId, tokenHash, now, cancellationToken);
        }

        return ToOutput(session.Id, account.Id, account.Name, invitation.OfferedRole, session.ExpiresOn);
    }

    private async Task<AcceptStaffInvitationOutput> ReplayAsync(
        IdempotencyRecord record,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!FingerprintsMatch(record.Fingerprint, fingerprint))
        {
            throw new StaffInvitationException(
                422,
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.");
        }

        if (record.StatusCode == StatusCodesLikeOk && record.StaffSessionId is Guid sessionId)
        {
            var active = await sessionStore.RenewActiveStaffSessionAsync(
                record.TenantId,
                sessionId,
                now,
                now.AddMinutes(sessionOptions.Value.InactivityTimeoutMinutes),
                cancellationToken);
            return active is null
                ? throw InvalidInvitation()
                : new AcceptStaffInvitationOutput(
                    active.SessionId,
                    active.AccountId,
                    active.Name,
                    active.Roles,
                    active.Permissions,
                    active.ExpiresAt);
        }

        throw InvalidInvitation();
    }

    private async Task<StaffInvitationException> ResolveWriteConflictAsync(
        Guid tenantId,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await invitationStore.FindByTokenHashAsync(tenantId, tokenHash, cancellationToken);
        if (!IsAcceptable(invitation, now))
        {
            return InvalidInvitation();
        }

        var accountExists = await staffAccountStore.FindActiveAccountByNormalizedEmailAsync(
            tenantId,
            invitation!.NormalizedEmail,
            cancellationToken);
        return accountExists is null ? InvalidInvitation() : EmailUnavailable();
    }

    private static bool IsAcceptable(StaffInvitation? invitation, DateTimeOffset now)
        => invitation is not null
            && invitation.ExpiresOn > now
            && invitation.AcceptedOn is null
            && invitation.SupersededOn is null;

    private static AcceptStaffInvitationOutput ToOutput(
        Guid sessionId,
        Guid accountId,
        string name,
        string role,
        DateTimeOffset expiresAt)
    {
        var roles = new[] { role };
        var permissions = roles
            .SelectMany(StaffRoleCatalog.GetPermissions)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AcceptStaffInvitationOutput(sessionId, accountId, name, roles, permissions, expiresAt);
    }

    private static StaffInvitationException EmailUnavailable()
        => new(422, "INVITATION_EMAIL_UNAVAILABLE", "The invitation account email is no longer available.");

    private static StaffInvitationException InvalidInvitation()
        => new(422, "INVITATION_INVALID", "This invitation is no longer valid.");

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool FingerprintsMatch(string storedFingerprint, string fingerprint)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedFingerprint),
            Convert.FromHexString(fingerprint));

    private const int StatusCodesLikeOk = 200;
}
