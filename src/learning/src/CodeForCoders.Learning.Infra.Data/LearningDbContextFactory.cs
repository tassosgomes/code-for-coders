using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.Learning.Infra.Data;

public sealed class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_learning;Username=code_for_coders_learning";
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                LearningSchemas.Content))
            .Options;

        return new LearningDbContext(options, new TenantContext());
    }
}
