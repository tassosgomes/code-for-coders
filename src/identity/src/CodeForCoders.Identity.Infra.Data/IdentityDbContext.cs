using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data;

public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Credential> Credentials => Set<Credential>();

    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(IdentitySchema.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(
            message => tenantContext.TenantId.HasValue && message.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Account>().HasQueryFilter(
            account => tenantContext.TenantId.HasValue && account.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<Credential>().HasQueryFilter(
            credential => tenantContext.TenantId.HasValue && credential.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<VerificationToken>().HasQueryFilter(
            token => tenantContext.TenantId.HasValue && token.TenantId == tenantContext.TenantId.Value);
        modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(
            record => tenantContext.TenantId.HasValue && record.TenantId == tenantContext.TenantId.Value);
    }
}
