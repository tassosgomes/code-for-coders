using CodeForCoders.Audit.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.Audit.Infra.Data;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var migrationConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MigrationConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_audit;Username=code_for_coders_audit";
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(migrationConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AuditSchema.Name))
            .Options;

        return new AuditDbContext(options);
    }
}
