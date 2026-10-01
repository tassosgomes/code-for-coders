using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class CatalogCourseEditStore(CommerceDbContext dbContext) : ICatalogCourseEditStore
{
    public async Task<ICatalogEditTransaction> LockAsync(CatalogEditScope scope, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // A receipt key spans courses: acquire this lock first for concurrent reuse across courses.
            var receiptKey = $"catalog-edit/{scope.TenantId:D}/{scope.ActorId:D}/{scope.Key}";
            var courseKey = $"{scope.TenantId:D}/{scope.CourseId:D}";
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({receiptKey}, 0))", cancellationToken);
            await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({courseKey}, 0))", cancellationToken);
            return new CatalogEditTransaction(transaction);
        }
        catch (OperationCanceledException) { await transaction.DisposeAsync(); throw; }
        catch (System.Data.Common.DbException) { await transaction.DisposeAsync(); throw; }
    }

    public Task<CatalogCourseView?> GetAsync(Guid courseId, CancellationToken cancellationToken)
        => dbContext.CatalogCourseViews.SingleOrDefaultAsync(course => course.CourseId == courseId, cancellationToken);

    public Task<CatalogEditReceipt?> FindAsync(CatalogEditScope scope, CancellationToken cancellationToken)
        => dbContext.CatalogEditReceipts.SingleOrDefaultAsync(receipt => receipt.TenantId == scope.TenantId
            && receipt.ActorId == scope.ActorId && receipt.Key == scope.Key, cancellationToken);

    public void Add(CatalogEditReceipt receipt) => dbContext.CatalogEditReceipts.Add(receipt);
}
