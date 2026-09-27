using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IOperationIdempotencyRepository
{
    Task<OperationIdempotencyRecord?> GetAsync(
        string operation,
        Guid tenantId,
        Guid actorAccountId,
        string key,
        CancellationToken cancellationToken);

    Task AddAsync(OperationIdempotencyRecord record, CancellationToken cancellationToken);
}
