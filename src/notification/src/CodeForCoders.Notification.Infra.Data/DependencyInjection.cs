using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.Repositories;
using CodeForCoders.Notification.Infra.Data.Adapters;
using CodeForCoders.Notification.Infra.Data.Configuration;
using CodeForCoders.Notification.Infra.Data.Health;
using CodeForCoders.Notification.Infra.Data.Outbox;
using CodeForCoders.Notification.Infra.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Notification.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                NotificationSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, NotificationUnitOfWork>();
        services.AddScoped<IDeliveryRecordRepository, DeliveryRecordRepository>();
        services.AddScoped<IEmailTemplateSettings, EmailTemplateSettings>();
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.ValidityHoursByPurpose.TryGetValue("confirmacao-de-conta", out var accountValidity)
                    && accountValidity > 0,
                "Email validity for account confirmation is required.")
            .ValidateOnStart();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();

        return services;
    }
}
