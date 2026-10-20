using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.IntegrationTests;

/// <summary>
/// Deterministic two-phase rendezvous used to interleave two isolated DbContext scopes in a test.
/// The paused operation trips the barrier at a chosen decision point and blocks until the test
/// releases it; the test observes <see cref="Reached"/> to know the operation is parked. No sleeps.
/// </summary>
internal sealed class ConcurrencyBarrier
{
    private readonly TaskCompletionSource _reached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _tripped;

    /// <summary>Completes once the paused operation first reaches the barrier.</summary>
    public Task Reached => _reached.Task;

    /// <summary>Trips the barrier from an async decision point and waits for the release.</summary>
    public async Task ReachAndWaitAsync()
    {
        if (Interlocked.Exchange(ref _tripped, 1) == 1)
        {
            return;
        }

        _reached.TrySetResult();
        await _released.Task;
    }

    /// <summary>Trips the barrier from a synchronous decision point (e.g. a store Add call).</summary>
    public void ReachAndWait()
    {
        if (Interlocked.Exchange(ref _tripped, 1) == 1)
        {
            return;
        }

        _reached.TrySetResult();
        _released.Task.GetAwaiter().GetResult();
    }

    public void Release() => _released.TrySetResult();
}

/// <summary>
/// RACE #7 probe: wraps the real staff account store and parks the acceptance right after the
/// "is the email still free?" query comes back null - the moment after the invitation was loaded
/// and judged acceptable, and just before the account is written.
/// </summary>
internal sealed class BarrieredStaffAccountStore(
    IIdentityStaffAccountStore inner,
    ConcurrencyBarrier barrier,
    bool parkAfterLoadingStaffSessions = false) : IIdentityStaffAccountStore
{
    public Task<int> CountInternalStaffMembersAsync(Guid tenantId, CancellationToken cancellationToken)
        => inner.CountInternalStaffMembersAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<StaffMemberRecord>> ListInternalStaffMembersAsync(
        Guid tenantId,
        int page,
        int size,
        CancellationToken cancellationToken)
        => inner.ListInternalStaffMembersAsync(tenantId, page, size, cancellationToken);

    public async Task<Account?> FindActiveAccountByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var account = await inner.FindActiveAccountByNormalizedEmailAsync(
            tenantId,
            normalizedEmail,
            cancellationToken);
        if (account is null && !parkAfterLoadingStaffSessions)
        {
            await barrier.ReachAndWaitAsync();
        }

        return account;
    }

    public Task<bool> HasAdministratorAsync(Guid tenantId, CancellationToken cancellationToken)
        => inner.HasAdministratorAsync(tenantId, cancellationToken);

    public Task<Account?> FindInternalAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => inner.FindInternalAccountAsync(tenantId, accountId, cancellationToken);

    public Task<StaffRoleAssignment?> FindRoleAssignmentAsync(
        Guid tenantId,
        Guid accountId,
        string role,
        CancellationToken cancellationToken)
        => inner.FindRoleAssignmentAsync(tenantId, accountId, role, cancellationToken);

    public async Task<List<StaffSession>> FindUnrevokedStaffSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var sessions = await inner.FindUnrevokedStaffSessionsAsync(tenantId, accountId, cancellationToken);
        if (parkAfterLoadingStaffSessions)
        {
            // RACE #8 (staff) reverse probe: the reset already rewrote the credential in memory and
            // loaded the sessions to revoke; the commit has not happened yet.
            await barrier.ReachAndWaitAsync();
        }

        return sessions;
    }

    public void AddAccount(Account account) => inner.AddAccount(account);

    public void AddCredential(Credential credential) => inner.AddCredential(credential);

    public void AddRoleAssignment(StaffRoleAssignment assignment) => inner.AddRoleAssignment(assignment);

    public void RemoveRoleAssignment(StaffRoleAssignment assignment) => inner.RemoveRoleAssignment(assignment);
}

/// <summary>
/// RACE #8 probe: wraps the real session store and parks the login right as it is about to stage
/// the new session row - after the credential hash was already verified, before the row exists.
/// </summary>
internal sealed class BarrieredSessionStore(
    IIdentitySessionStore inner,
    ConcurrencyBarrier barrier,
    bool parkOnStaffSession = false) : IIdentitySessionStore
{
    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken)
        => inner.FindIdempotencyAsync(tenantId, operationId, keyHash, cancellationToken);

    public Task<(Account? Account, Credential? Credential)> FindStudentCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => inner.FindStudentCredentialAsync(tenantId, normalizedEmail, cancellationToken);

    public Task<(Account? Account, Credential? Credential)> FindInternalCredentialAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => inner.FindInternalCredentialAsync(tenantId, normalizedEmail, cancellationToken);

    public Task<IReadOnlyList<string>> GetStaffRolesAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => inner.GetStaffRolesAsync(tenantId, accountId, cancellationToken);

    public Task<StudentSessionDetails?> RenewActiveSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
        => inner.RenewActiveSessionAsync(tenantId, sessionId, now, expiresOn, cancellationToken);

    public Task<StaffSessionDetails?> RenewActiveStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
        => inner.RenewActiveStaffSessionAsync(tenantId, sessionId, now, expiresOn, cancellationToken);

    public Task RevokeSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken)
        => inner.RevokeSessionAsync(tenantId, sessionId, revokedOn, cancellationToken);

    public Task RevokeStaffSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset revokedOn,
        CancellationToken cancellationToken)
        => inner.RevokeStaffSessionAsync(tenantId, sessionId, revokedOn, cancellationToken);

    public void AddSession(StudentSession session)
    {
        if (!parkOnStaffSession)
        {
            barrier.ReachAndWait();
        }

        inner.AddSession(session);
    }

    public void AddStaffSession(StaffSession session)
    {
        if (parkOnStaffSession)
        {
            barrier.ReachAndWait();
        }

        inner.AddStaffSession(session);
    }

    public void AddIdempotencyRecord(IdempotencyRecord record) => inner.AddIdempotencyRecord(record);

    public void TouchAuthenticatedCredential(Credential credential) => inner.TouchAuthenticatedCredential(credential);
}

/// <summary>
/// RACE #8 reverse probe: wraps the real recovery store and parks the password reset right after
/// it loaded the sessions to revoke - the credential was already read and rewritten in memory, and
/// the commit has not happened yet. A login that commits meanwhile re-stages the same credential.
/// </summary>
internal sealed class BarrieredPasswordRecoveryStore(
    IIdentityPasswordRecoveryStore inner,
    ConcurrencyBarrier barrier) : IIdentityPasswordRecoveryStore
{
    public Task<IdempotencyRecord?> FindIdempotencyAsync(
        Guid tenantId,
        string operationId,
        string keyHash,
        CancellationToken cancellationToken)
        => inner.FindIdempotencyAsync(tenantId, operationId, keyHash, cancellationToken);

    public Task<Account?> FindEligibleStudentByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => inner.FindEligibleStudentByEmailAsync(tenantId, normalizedEmail, cancellationToken);

    public Task<Account?> FindEligibleStaffByEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => inner.FindEligibleStaffByEmailAsync(tenantId, normalizedEmail, cancellationToken);

    public Task<VerificationToken?> FindVerificationTokenAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken)
        => inner.FindVerificationTokenAsync(tenantId, tokenHash, cancellationToken);

    public Task<Account?> FindStudentAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => inner.FindStudentAccountAsync(tenantId, accountId, cancellationToken);

    public Task<Credential?> FindCredentialAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
        => inner.FindCredentialAsync(tenantId, accountId, cancellationToken);

    public Task<StudentSession?> FindActiveStudentSessionAsync(
        Guid tenantId,
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => inner.FindActiveStudentSessionAsync(tenantId, sessionId, now, cancellationToken);

    public Task<List<VerificationToken>> FindUnconsumedVerificationTokensAsync(
        Guid tenantId,
        Guid accountId,
        string purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => inner.FindUnconsumedVerificationTokensAsync(tenantId, accountId, purpose, now, cancellationToken);

    public async Task<List<StudentSession>> FindUnrevokedStudentSessionsAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var sessions = await inner.FindUnrevokedStudentSessionsAsync(tenantId, accountId, cancellationToken);
        await barrier.ReachAndWaitAsync();
        return sessions;
    }

    public void AddVerificationToken(VerificationToken token) => inner.AddVerificationToken(token);

    public void AddCredential(Credential credential) => inner.AddCredential(credential);

    public void AddIdempotencyRecord(IdempotencyRecord record) => inner.AddIdempotencyRecord(record);
}
