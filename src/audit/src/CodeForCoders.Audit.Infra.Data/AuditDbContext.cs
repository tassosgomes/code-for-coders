using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditDbContext(
    DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAppendOnly()
    {
        if (ChangeTracker.Entries<AuditRecord>().Any(entry => AuditAppendOnlyPolicy.IsMutation(entry.State)))
        {
            throw new InvalidOperationException(AuditAppendOnlyPolicy.MutationRejectedMessage);
        }
    }
}
