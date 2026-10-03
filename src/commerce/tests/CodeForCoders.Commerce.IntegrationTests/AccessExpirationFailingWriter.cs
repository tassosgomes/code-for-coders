using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class AccessExpirationFailingWriter(CommerceDbContext db) : IEntitlementOutboxMessageWriter
{
    private int appended;

    public async Task AppendAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        await new EntitlementOutboxMessageWriter(db).AppendAsync(message, cancellationToken);
        if (++appended != 2) return;
        // Flush inside the cycle transaction, then fail: the persisted first marker and both facts must roll back.
        await db.SaveChangesAsync(cancellationToken);
        throw new DbUpdateException("Simulated expiration batch failure.");
    }
}
