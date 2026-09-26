using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;
using CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Domain.Exceptions;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

public sealed class StaffRoleActionExecutor(
    IIdentityStaffAccountStore staffAccountStore,
    IIdentitySessionStore sessionStore,
    IStaffRoleMessageWriter messageWriter,
    IUnitOfWork unitOfWork,
    IIdempotencyFingerprinter fingerprinter,
    TimeProvider timeProvider,
    IValidator<StaffRoleActionCommand> validator,
    IValidator<StaffRoleChangeCommand> changeValidator)
{
    private const int OkStatusCode = 200;
    private const int NotFoundStatusCode = 404;
    private const int UnprocessableEntityStatusCode = 422;
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public Task<StaffRoleActionOutput> GrantAsync(StaffRoleActionCommand input, CancellationToken cancellationToken)
        => ExecuteAsync(input, revoke: false, cancellationToken);

    public Task<StaffRoleActionOutput> RevokeAsync(StaffRoleActionCommand input, CancellationToken cancellationToken)
        => ExecuteAsync(input, revoke: true, cancellationToken);

    public Task<StaffRoleActionOutput> ChangeAsync(StaffRoleChangeCommand input, CancellationToken cancellationToken)
        => ExecuteChangeAsync(input, cancellationToken, retryWriteConflict: true);

    private async Task<StaffRoleActionOutput> ExecuteAsync(
        StaffRoleActionCommand input,
        bool revoke,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.ActorAccountId == input.TargetAccountId)
        {
            throw Failure(UnprocessableEntityStatusCode, "SELF_ROLE_CHANGE_FORBIDDEN", "An actor cannot change their own roles.");
        }

        if (!StaffRoleCatalog.Contains(input.Role))
        {
            throw Failure(UnprocessableEntityStatusCode, "ROLE_NOT_SUPPORTED", "The requested role is not supported.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw Failure(UnprocessableEntityStatusCode, "REASON_REQUIRED", "A reason is required for staff role changes.");
        }

        var now = timeProvider.GetUtcNow();
        var operationId = revoke ? "revokeStaffRoleInternal" : "grantStaffRoleInternal";
        var keyHash = fingerprinter.HashKey($"{input.ActorAccountId:D}:{input.IdempotencyKey}");
        var fingerprint = fingerprinter.Fingerprint(JsonSerializer.Serialize(new
        {
            input.TargetAccountId,
            input.Role,
            input.Reason,
        }));
        var existingRecord = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            operationId,
            keyHash,
            cancellationToken);
        if (existingRecord is not null && !existingRecord.IsExpired(now))
        {
            return await ReplayOrConflictAsync(existingRecord, input, fingerprint, revoke, cancellationToken);
        }

        var target = await staffAccountStore.FindInternalAccountAsync(
            input.TenantId,
            input.TargetAccountId,
            cancellationToken);
        if (target is null)
        {
            throw Failure(NotFoundStatusCode, "STAFF_MEMBER_NOT_FOUND", "The staff member was not found.");
        }

        var assignment = await staffAccountStore.FindRoleAssignmentAsync(
            input.TenantId,
            input.TargetAccountId,
            input.Role,
            cancellationToken);
        var changed = revoke ? assignment is not null : assignment is null;
        if (changed)
        {
            if (revoke)
            {
                staffAccountStore.RemoveRoleAssignment(assignment!);
                var sessions = await staffAccountStore.FindUnrevokedStaffSessionsAsync(
                    input.TenantId,
                    input.TargetAccountId,
                    cancellationToken);
                foreach (var session in sessions)
                {
                    session.Revoke(now);
                }

                await messageWriter.AppendRoleRevokedAsync(
                    input.TenantId,
                    input.ActorAccountId,
                    input.TargetAccountId,
                    input.Role,
                    input.Reason,
                    now,
                    correlationId: null,
                    cancellationToken);
            }
            else
            {
                staffAccountStore.AddRoleAssignment(StaffRoleAssignment.Create(
                    Guid.CreateVersion7(now),
                    input.TenantId,
                    input.TargetAccountId,
                    input.Role,
                    now));
                await messageWriter.AppendRoleGrantedAsync(
                    input.TenantId,
                    input.ActorAccountId,
                    input.TargetAccountId,
                    input.Role,
                    input.Reason,
                    now,
                    correlationId: null,
                    cancellationToken);
            }
        }

        var output = changed
            ? await BuildChangedOutputAsync(target, input, revoke, cancellationToken)
            : await BuildOutputAsync(target, input.ActorAccountId, changed, revoke, cancellationToken);
        var record = PrepareIdempotencyRecord(
            existingRecord,
            input.TenantId,
            operationId,
            keyHash,
            fingerprint,
            now,
            changed);
        if (existingRecord is null)
        {
            sessionStore.AddIdempotencyRecord(record);
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName is "ux_idempotency_records_scope_key"
                or "ux_staff_role_assignments_tenant_account_role")
        {
            var winner = await sessionStore.FindIdempotencyAsync(
                input.TenantId,
                operationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ReplayOrConflictAsync(winner, input, fingerprint, revoke, cancellationToken);
            }

            if (exception.ConstraintName != "ux_staff_role_assignments_tenant_account_role")
            {
                throw;
            }

            return await ResolveConcurrentNoChangeAsync(
                input,
                operationId,
                keyHash,
                fingerprint,
                now,
                revoke,
                cancellationToken);
        }
        catch (ConcurrentWriteException) when (revoke)
        {
            return await ResolveConcurrentNoChangeAsync(
                input,
                operationId,
                keyHash,
                fingerprint,
                now,
                revoke,
                cancellationToken);
        }

        return output;
    }

    private async Task<StaffRoleActionOutput> ExecuteChangeAsync(
        StaffRoleChangeCommand input,
        CancellationToken cancellationToken,
        bool retryWriteConflict)
    {
        await changeValidator.ValidateAndThrowAsync(input, cancellationToken);
        if (input.ActorAccountId == input.TargetAccountId)
        {
            throw Failure(UnprocessableEntityStatusCode, "SELF_ROLE_CHANGE_FORBIDDEN", "An actor cannot change their own roles.");
        }

        if (!StaffRoleCatalog.Contains(input.FromRole) || !StaffRoleCatalog.Contains(input.ToRole))
        {
            throw Failure(UnprocessableEntityStatusCode, "ROLE_NOT_SUPPORTED", "The requested role is not supported.");
        }

        if (string.Equals(input.FromRole, input.ToRole, StringComparison.Ordinal))
        {
            throw Failure(UnprocessableEntityStatusCode, "ROLE_CHANGE_INVALID", "The source and destination roles must be different.");
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw Failure(UnprocessableEntityStatusCode, "REASON_REQUIRED", "A reason is required for staff role changes.");
        }

        var now = timeProvider.GetUtcNow();
        var operationId = "changeStaffRoleInternal";
        var keyHash = fingerprinter.HashKey($"{input.ActorAccountId:D}:{input.IdempotencyKey}");
        var fingerprint = fingerprinter.Fingerprint(JsonSerializer.Serialize(new
        {
            input.TargetAccountId,
            input.FromRole,
            input.ToRole,
            input.Reason,
        }));
        var existingRecord = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            operationId,
            keyHash,
            cancellationToken);
        var replayInput = ToRoleActionCommand(input);
        if (existingRecord is not null && !existingRecord.IsExpired(now))
        {
            return await ReplayOrConflictAsync(existingRecord, replayInput, fingerprint, revoke: true, cancellationToken);
        }

        var target = await staffAccountStore.FindInternalAccountAsync(
            input.TenantId,
            input.TargetAccountId,
            cancellationToken);
        if (target is null)
        {
            throw Failure(NotFoundStatusCode, "STAFF_MEMBER_NOT_FOUND", "The staff member was not found.");
        }

        var fromAssignment = await staffAccountStore.FindRoleAssignmentAsync(
            input.TenantId,
            input.TargetAccountId,
            input.FromRole,
            cancellationToken);
        if (fromAssignment is null)
        {
            throw Failure(UnprocessableEntityStatusCode, "ROLE_NOT_HELD", "The staff member does not hold the source role.");
        }

        var toAssignment = await staffAccountStore.FindRoleAssignmentAsync(
            input.TenantId,
            input.TargetAccountId,
            input.ToRole,
            cancellationToken);
        staffAccountStore.RemoveRoleAssignment(fromAssignment);
        if (toAssignment is null)
        {
            staffAccountStore.AddRoleAssignment(StaffRoleAssignment.Create(
                Guid.CreateVersion7(now),
                input.TenantId,
                input.TargetAccountId,
                input.ToRole,
                now));
        }

        var sessions = await staffAccountStore.FindUnrevokedStaffSessionsAsync(
            input.TenantId,
            input.TargetAccountId,
            cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        var correlationId = Activity.Current?.Id ?? Guid.CreateVersion7(now).ToString("D");
        await messageWriter.AppendRoleRevokedAsync(
            input.TenantId,
            input.ActorAccountId,
            input.TargetAccountId,
            input.FromRole,
            input.Reason,
            now,
            correlationId,
            cancellationToken);
        if (toAssignment is null)
        {
            await messageWriter.AppendRoleGrantedAsync(
                input.TenantId,
                input.ActorAccountId,
                input.TargetAccountId,
                input.ToRole,
                input.Reason,
                now,
                correlationId,
                cancellationToken);
        }

        var currentOutput = await BuildOutputAsync(
            target,
            input.ActorAccountId,
            changed: true,
            revoke: true,
            cancellationToken);
        var roles = currentOutput.Member.Roles
            .Where(role => !string.Equals(role, input.FromRole, StringComparison.Ordinal))
            .Append(input.ToRole)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var output = currentOutput with
        {
            Member = currentOutput.Member with { Roles = roles },
        };
        var record = PrepareIdempotencyRecord(
            existingRecord,
            input.TenantId,
            operationId,
            keyHash,
            fingerprint,
            now,
            changed: true);
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
                operationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ReplayOrConflictAsync(winner, replayInput, fingerprint, revoke: true, cancellationToken);
            }

            throw;
        }
        catch (RegistrationWriteConflictException exception)
            when (exception.ConstraintName == "ux_staff_role_assignments_tenant_account_role" && retryWriteConflict)
        {
            return await ExecuteChangeAsync(input, cancellationToken, retryWriteConflict: false);
        }
        catch (ConcurrentWriteException) when (retryWriteConflict)
        {
            return await ExecuteChangeAsync(input, cancellationToken, retryWriteConflict: false);
        }

        return output;
    }

    private async Task<StaffRoleActionOutput> ResolveConcurrentNoChangeAsync(
        StaffRoleActionCommand input,
        string operationId,
        string keyHash,
        string fingerprint,
        DateTimeOffset now,
        bool revoke,
        CancellationToken cancellationToken)
    {
        var assignment = await staffAccountStore.FindRoleAssignmentAsync(
            input.TenantId,
            input.TargetAccountId,
            input.Role,
            cancellationToken);
        if (revoke ? assignment is not null : assignment is null)
        {
            throw new InvalidOperationException("The staff role write conflict did not resolve to a no-op.");
        }

        var target = await staffAccountStore.FindInternalAccountAsync(
            input.TenantId,
            input.TargetAccountId,
            cancellationToken);
        if (target is null)
        {
            throw Failure(NotFoundStatusCode, "STAFF_MEMBER_NOT_FOUND", "The staff member was not found.");
        }

        var output = await BuildOutputAsync(target, input.ActorAccountId, changed: false, revoke, cancellationToken);
        var existingRecord = await sessionStore.FindIdempotencyAsync(
            input.TenantId,
            operationId,
            keyHash,
            cancellationToken);
        if (existingRecord is not null && !existingRecord.IsExpired(now))
        {
            return await ReplayOrConflictAsync(existingRecord, input, fingerprint, revoke, cancellationToken);
        }

        var record = PrepareIdempotencyRecord(
            existingRecord,
            input.TenantId,
            operationId,
            keyHash,
            fingerprint,
            now,
            changed: false);
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
                operationId,
                keyHash,
                cancellationToken);
            if (winner is not null && !winner.IsExpired(now))
            {
                return await ReplayOrConflictAsync(winner, input, fingerprint, revoke, cancellationToken);
            }

            throw;
        }

        return output;
    }

    private async Task<StaffRoleActionOutput> ReplayOrConflictAsync(
        IdempotencyRecord record,
        StaffRoleActionCommand input,
        string fingerprint,
        bool revoke,
        CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(record.Fingerprint),
                Convert.FromHexString(fingerprint))
            || record.StaffRoleActionChanged is not bool changed)
        {
            throw Failure(UnprocessableEntityStatusCode, "IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different request.");
        }

        var target = await staffAccountStore.FindInternalAccountAsync(
            input.TenantId,
            input.TargetAccountId,
            cancellationToken);
        if (target is null)
        {
            throw Failure(NotFoundStatusCode, "STAFF_MEMBER_NOT_FOUND", "The staff member was not found.");
        }

        return await BuildOutputAsync(target, input.ActorAccountId, changed, revoke, cancellationToken);
    }

    private async Task<StaffRoleActionOutput> BuildOutputAsync(
        Account target,
        Guid actorAccountId,
        bool changed,
        bool revoke,
        CancellationToken cancellationToken)
    {
        var roles = await sessionStore.GetStaffRolesAsync(target.TenantId, target.Id, cancellationToken);
        return new StaffRoleActionOutput(
            new StaffMemberOutput(target.Id, target.Name, target.Email, roles, target.Id == actorAccountId),
            changed,
            revoke && changed);
    }

    private async Task<StaffRoleActionOutput> BuildChangedOutputAsync(
        Account target,
        StaffRoleActionCommand input,
        bool revoke,
        CancellationToken cancellationToken)
    {
        var output = await BuildOutputAsync(target, input.ActorAccountId, changed: true, revoke, cancellationToken);
        var roles = revoke
            ? output.Member.Roles.Where(role => !string.Equals(role, input.Role, StringComparison.Ordinal)).ToArray()
            : output.Member.Roles.Append(input.Role).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return output with { Member = output.Member with { Roles = roles } };
    }

    private static IdempotencyRecord PrepareIdempotencyRecord(
        IdempotencyRecord? existingRecord,
        Guid tenantId,
        string operationId,
        string keyHash,
        string fingerprint,
        DateTimeOffset now,
        bool changed)
    {
        var record = existingRecord ?? IdempotencyRecord.Create(
            tenantId,
            operationId,
            keyHash,
            fingerprint,
            OkStatusCode,
            null,
            null,
            now,
            now.Add(IdempotencyWindow));
        if (existingRecord is not null)
        {
            record.Refresh(fingerprint, OkStatusCode, null, null, now, now.Add(IdempotencyWindow));
        }

        record.SetStaffRoleActionResult(changed);
        return record;
    }

    private static StaffRoleActionCommand ToRoleActionCommand(StaffRoleChangeCommand input)
        => new(
            input.TenantId,
            input.ActorAccountId,
            input.TargetAccountId,
            input.FromRole,
            input.Reason,
            input.IdempotencyKey);

    private static StaffRoleActionException Failure(int statusCode, string code, string title)
        => new(statusCode, code, title);
}
