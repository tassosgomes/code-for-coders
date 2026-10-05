using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.Billing.Infra.Data;

public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_billing;Username=code_for_coders_billing";
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BillingSchema.Name))
            .Options;

        return new BillingDbContext(options, new TenantContext());
    }
}
