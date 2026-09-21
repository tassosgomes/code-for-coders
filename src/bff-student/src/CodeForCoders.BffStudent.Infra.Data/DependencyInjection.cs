using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using CodeForCoders.BffStudent.Infra.Data.Configuration;
using CodeForCoders.BffStudent.Infra.Data.Health;
using CodeForCoders.BffStudent.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.BffStudent.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<BffStudentDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BffStudentSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, BffStudentUnitOfWork>();
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
