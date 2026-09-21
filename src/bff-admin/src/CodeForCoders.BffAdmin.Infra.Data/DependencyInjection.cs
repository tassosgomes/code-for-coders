using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Infra.Data.Configuration;
using CodeForCoders.BffAdmin.Infra.Data.Health;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.BffAdmin.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<BffAdminDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BffAdminSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, BffAdminUnitOfWork>();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<BffSecurityOptions>()
            .Bind(configuration.GetSection(BffSecurityOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.UseOpaqueSessions, "BFF sessions must be opaque.")
            .Validate(options => !options.BrowserReceivesAccessToken, "Access tokens must remain server-side.")
            .Validate(options => string.Equals(options.ReverseProxy, "YARP", StringComparison.Ordinal), "YARP is the required reverse proxy.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();
        services.AddSingleton<IBffSessionStore, ValkeyBffSessionStore>();

        return services;
    }
}
