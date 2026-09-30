using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CodeForCoders.Learning.Infra.Data.Idempotency;

public sealed class CourseCreationTransaction(IDbContextTransaction transaction) : ICourseCreationTransaction
{
    public Task CompleteAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
