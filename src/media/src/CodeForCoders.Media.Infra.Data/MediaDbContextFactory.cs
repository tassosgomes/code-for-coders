using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForCoders.Media.Infra.Data;

public sealed class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=code_for_coders_media;Username=code_for_coders_media";
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                MediaSchema.Name))
            .Options;

        return new MediaDbContext(options, new TenantContext());
    }
}
