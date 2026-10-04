using CodeForCoders.Learning.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Messaging;

internal static class ProgressProjectionLock
{
    public static Task AcquireAsync(LearningDbContext dbContext, string key, CancellationToken cancellationToken)
        // Both arrival orders must observe the other transaction, including when no duration row exists yet.
        => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
}
