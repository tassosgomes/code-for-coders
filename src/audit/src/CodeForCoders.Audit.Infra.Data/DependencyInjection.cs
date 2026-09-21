using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Infra.Data.Configuration;
using CodeForCoders.Audit.Infra.Data.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Audit.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                AuditSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();
        services.AddScoped<IUnitOfWork, AuditUnitOfWork>();
        services.AddOptions<AuditDatabaseOptions>()
            .Bind(configuration.GetSection(AuditDatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.WriterRole), "Audit writer role is required.")
            .ValidateOnStart();

        return services;
    }
}
