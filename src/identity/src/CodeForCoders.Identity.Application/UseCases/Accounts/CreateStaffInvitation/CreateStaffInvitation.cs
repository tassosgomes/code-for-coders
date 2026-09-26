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

namespace CodeForCoders.Identity.Application.UseCases.Accounts.CreateStaffInvitation;

public sealed class CreateStaffInvitation(
    IIdentityStaffAccountStore staffAccountStore,
    IIdentityStaffInvitationStore invitationStore,
    IIdentitySessionStore sessionStore,
    IStaffInvitationMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    IOptions<StaffInvitationOptions> invitationOptions,
    TimeProvider timeProvider,
    IValidator<CreateStaffInvitationInput> validator) : ICreateStaffInvitation
{
    public const string OperationId = "createStaffInvitationInternal";
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);
    private const int CreatedStatusCode = 201;
    private const int UnprocessableEntityStatusCode = 422;

    public async Task<CreateStaffInvitationOutput> ExecuteAsync(
        CreateStaffInvitationInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var email = input.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();
        var role = input.Role.Trim();
        var reason = input.Reason.Trim();
        if (reason.Length == 0)
        {
            throw Failure("REASON_REQUIRED", "An invitation reason is required.");
        }

        if (!StaffRoleCatalog.Contains(role))
        {
            throw Failure("ROLE_NOT_SUPPORTED", "The offered role is not supported.");
        }

        var currentTime = timeProvider.GetUtcNow();
        var now = new DateTimeOffset(
            currentTime.UtcTicks - (currentTime.UtcTicks % 10),
            TimeSpan.Zero);
        var keyHash = fingerprinter.HashKey(input.IdempotencyKey);
        var fingerprint = fingerprinter.Fingerprint(JsonSerializer.Serialize(new
        {
            Email = normalizedEmail,
            Role = role,
            Reason = reason,
        }));
        var existingRecord = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            OperationId,
            keyHash,
            cancellationToken);
        if (existingRecord is not null && !existingRecord.IsExpired(now))
        {
            return await ReplayOrConflictAsync(existingRecord, fingerprint, input.TenantId, cancellationToken);
        }

        var existingAccount = await staffAccountStore.FindActiveAccountByNormalizedEmailAsync(
            input.TenantId,
            normalizedEmail,
            cancellationToken);
        if (existingAccount is not null)
        {
            var code = existingAccount.Type == AccountType.Student
                ? "EMAIL_BELONGS_TO_STUDENT"
                : "EMAIL_BELONGS_TO_STAFF";
            throw Failure(code, "The email already belongs to an account.");
        }

        var previousInvitation = await invitationStore.FindUnresolvedByNormalizedEmailAsync(
            input.TenantId,
            normalizedEmail,
            cancellationToken);
        if (previousInvitation is not null)
        {
            previousInvitation.Supersede(now);
        }

        var rawToken = CreateToken();
        var id = Guid.CreateVersion7(now);
        var invitation = StaffInvitation.Create(
            id,
            input.TenantId,
            email,
            normalizedEmail,
            role,
            HashToken(rawToken),
            now,
            now.AddHours(invitationOptions.Value.LifetimeHours));
        invitationStore.Add(invitation);
        await messageWriter.AppendInvitationIssuedAsync(
            invitation,
            rawToken,
            input.ActorAccountId,
            reason,
            now,
            cancellationToken);

        var record = existingRecord ?? IdempotencyRecord.Create(
            input.TenantId,
            OperationId,
            keyHash,
            fingerprint,
            CreatedStatusCode,
            null,
            null,
            now,
            now.Add(IdempotencyWindow));
        if (existingRecord is not null)
        {
            record.Refresh(fingerprint, CreatedStatusCode, null, null, now, now.Add(IdempotencyWindow));
        }

        record.SetStaffInvitationResult(invitation.Id, previousInvitation?.Id);
        if (existingRecord is null)
        {
            sessionStore.AddIdempotencyRecord(record);
        }

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
                return await ReplayOrConflictAsync(winner, fingerprint, input.TenantId, cancellationToken);
            }

            throw;
        }

        return ToOutput(invitation, previousInvitation?.Id);
    }

    private async Task<CreateStaffInvitationOutput> ReplayOrConflictAsync(
        IdempotencyRecord record,
        string fingerprint,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.Fingerprint),
                Convert.FromHexString(fingerprint)))
        {
            throw Failure("IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different request.");
        }

        if (record.StaffInvitationId is not Guid invitationId)
        {
            throw Failure("IDEMPOTENCY_CONFLICT", "The idempotency key has no invitation result.");
        }

        var invitation = await invitationStore.FindByIdAsync(tenantId, invitationId, cancellationToken);
        if (invitation is null)
        {
            throw Failure("IDEMPOTENCY_CONFLICT", "The invitation result is no longer available.");
        }

        return ToOutput(invitation, record.SupersededStaffInvitationId);
    }

    private static CreateStaffInvitationOutput ToOutput(StaffInvitation invitation, Guid? supersededInvitationId)
        => new(
            invitation.Id,
            invitation.Email,
            invitation.OfferedRole,
            invitation.InvitedOn,
            invitation.ExpiresOn,
            supersededInvitationId);

    private static StaffInvitationException Failure(string code, string title)
        => new(UnprocessableEntityStatusCode, code, title);

    private static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
