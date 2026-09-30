using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Infra.Data.Idempotency;

public sealed class CourseEditStore(LearningDbContext dbContext) : ICourseEditStore
{
    public async Task<ICourseCreationTransaction> LockAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({courseId.ToString()}, 0))", cancellationToken);
            return new CourseCreationTransaction(transaction);
        }
        catch (OperationCanceledException) { await transaction.DisposeAsync(); throw; }
        catch (System.Data.Common.DbException) { await transaction.DisposeAsync(); throw; }
    }
    public Task<CourseEditReceipt?> FindAsync(CourseEditScope scope, CancellationToken cancellationToken)
        => dbContext.CourseEditReceipts.SingleOrDefaultAsync(receipt => receipt.TenantId == scope.TenantId
            && receipt.ActorId == scope.ActorId && receipt.Key == scope.Key, cancellationToken);

    public void Add(CourseEditReceipt receipt) => dbContext.CourseEditReceipts.Add(receipt);
}
