using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class CourtesyGrantStore(CommerceDbContext db, ITenantContext tenant) : ICourtesyGrantStore
{
    public Task<GrantReceipt?> FindReceiptAsync(GrantScope scope, CancellationToken cancellationToken)
        => db.GrantReceipts.SingleOrDefaultAsync(item => item.TenantId == scope.TenantId && item.ActorId == scope.ActorId && item.KeyHash == scope.KeyHash, cancellationToken);
    public Task<string?> FindCourseTitleAsync(Guid courseId, CancellationToken cancellationToken)
        => db.EntitlementCourseViews.Where(item => item.CourseId == courseId).Select(item => item.Title).SingleOrDefaultAsync(cancellationToken);
    public Task<AccessGrant?> GetAsync(Guid grantId, CancellationToken cancellationToken)
        => db.AccessGrants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == grantId, cancellationToken);
    public void Add(AccessGrant grant) => db.AccessGrants.Add(grant);
    public void Add(GrantReceipt receipt)
    {
        if (db.Entry(receipt).State == EntityState.Detached) db.GrantReceipts.Add(receipt);
    }

    public async Task<IGrantTransaction> LockAsync(GrantScope scope, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var key = $"grant/{scope.TenantId:D}/{scope.ActorId:D}/{scope.KeyHash}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            return new GrantTransaction(transaction);
        }
        catch (OperationCanceledException) { await transaction.DisposeAsync(); throw; }
        catch (System.Data.Common.DbException) { await transaction.DisposeAsync(); throw; }
    }

    public async Task<Enrollment> GetOrCreateEnrollmentAsync(Guid studentId, Guid courseId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = $"enrollment/{tenant.TenantId:D}/{studentId:D}/{courseId:D}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(item => item.StudentId == studentId && item.CourseId == courseId, cancellationToken);
        if (enrollment is not null) return enrollment;
        enrollment = Enrollment.Create(tenant.TenantId!.Value, studentId, courseId, now);
        db.Enrollments.Add(enrollment);
        return enrollment;
    }
}
