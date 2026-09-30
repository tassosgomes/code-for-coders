using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Idempotency;

public sealed class CourseCreationStore(LearningDbContext dbContext) : ICourseCreationStore
{
    public async Task<ICourseCreationTransaction> LockAsync(CourseCreationScope scope, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.TenantId:D}:{scope.ActorId:D}:create-course:{scope.Key}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(hash);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
            return new CourseCreationTransaction(transaction);
        }
        catch (OperationCanceledException)
        {
            await transaction.DisposeAsync();
            throw;
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public Task<CourseCreationReceipt?> FindAsync(CourseCreationScope scope, CancellationToken cancellationToken)
        => dbContext.CourseCreationReceipts.SingleOrDefaultAsync(receipt => receipt.TenantId == scope.TenantId
            && receipt.ActorId == scope.ActorId && receipt.Key == scope.Key, cancellationToken);

    public void Add(CourseCreationReceipt receipt) => dbContext.CourseCreationReceipts.Add(receipt);
}
