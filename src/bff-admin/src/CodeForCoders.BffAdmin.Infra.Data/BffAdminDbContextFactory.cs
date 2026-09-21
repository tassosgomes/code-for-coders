using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.BffAdmin.Infra.Data;

public sealed class BffAdminDbContextFactory : IDesignTimeDbContextFactory<BffAdminDbContext>
{
    public BffAdminDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_bff_admin;Username=code_for_coders_bff_admin";
        var options = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BffAdminSchema.Name))
            .Options;

        return new BffAdminDbContext(options, new TenantContext());
    }
}
