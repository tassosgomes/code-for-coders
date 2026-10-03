using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyFailingCommit(CommerceDbContext db) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken); throw new InvalidOperationException("Controlled failure before transaction commit.");
    }
}
