using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Infra.Data.Configuration;
using CodeForCoders.Billing.Infra.Data.Health;
using CodeForCoders.Billing.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Billing.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<BillingDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                BillingSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                // Never expose payment descriptions or references in SQL logs.
            }
        });
        services.AddHostedService<Payments.GatewayInboxWorker>();
        services.AddScoped<IPaymentStore, Payments.PaymentStore>();
        services.AddScoped<IServiceAssertionReplayStore, ServiceAssertionReplayStore>();
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, BillingUnitOfWork>();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();

        return services;
    }
}
