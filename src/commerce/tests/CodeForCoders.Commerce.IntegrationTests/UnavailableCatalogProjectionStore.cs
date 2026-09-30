using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Npgsql;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class UnavailableCatalogProjectionStore : ICatalogCourseProjectionStore
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);

    public Task<bool> ApplyAsync(PublishedCourseSnapshot snapshot, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return Task.FromException<bool>(new NpgsqlException("Synthetic database connectivity failure."));
    }
}
