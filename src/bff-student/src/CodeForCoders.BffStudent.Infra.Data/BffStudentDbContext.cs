using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using CodeForCoders.BffStudent.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.BffStudent.Infra.Data;

public sealed class BffStudentDbContext(
    DbContextOptions<BffStudentDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BffStudentSchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BffStudentDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
    }
}
