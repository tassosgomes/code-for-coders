using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Idempotency;

public sealed class OperationIdempotencyRepository(MediaDbContext dbContext) : IOperationIdempotencyRepository
{
    public Task<OperationIdempotencyRecord?> GetAsync(
        string operation,
        Guid tenantId,
        Guid actorAccountId,
        string key,
        CancellationToken cancellationToken)
        => dbContext.OperationIdempotencyRecords.SingleOrDefaultAsync(
            record => record.TenantId == tenantId
                && record.ActorAccountId == actorAccountId
                && record.Operation == operation
                && record.Key == key,
            cancellationToken);

    public Task AddAsync(OperationIdempotencyRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.OperationIdempotencyRecords.Add(record);
        return Task.CompletedTask;
    }
}
