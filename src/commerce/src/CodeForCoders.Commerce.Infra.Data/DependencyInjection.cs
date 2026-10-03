using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using CodeForCoders.Commerce.Infra.Data.Health;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Commerce.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<CommerceDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                CommerceSchemas.Catalog));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                // Reasons are confidential even in development: never log SQL parameter values.
            }
        });
        services.AddScoped<ICourtesyGrantStore, Entitlement.CourtesyGrantStore>();
        services.AddScoped<IEntitlementOutboxMessageWriter, EntitlementOutboxMessageWriter>();
        services.AddHostedService<Entitlement.GrantReceiptCleanupWorker>();
        services.AddOptions<AccessExpirationOptions>()
            .Bind(configuration.GetSection(AccessExpirationOptions.SectionName))
            .Validate(options => options.PollingIntervalSeconds is > 0 and <= 1800, "Access expiration polling must be between 1 and 1800 seconds.")
            .Validate(options => options.BatchSize is > 0 and <= 1000, "Access expiration batch size must be between 1 and 1000.")
            .ValidateOnStart();
        services.AddScoped<Entitlement.AccessExpirationCycle>();
        services.AddHostedService<Entitlement.AccessExpirationWorker>();
        services.AddScoped<ICatalogCourseQueries, Queries.CatalogCourseQueries>();
        services.AddScoped<ICourtesyCourseQueries, Queries.CourtesyCourseQueries>();
        services.AddScoped<IStudentAccessGrantQueries, Queries.StudentAccessGrantQueries>();
        services.AddScoped<IAccessDecisionQueries, Entitlement.AccessDecisionQueries>();
        services.AddScoped<IEntitlementCourseProjectionStore, Entitlement.EntitlementCourseProjectionStore>();
        services.AddScoped<IOfferReferenceQueries, Queries.OfferReferenceQueries>();
        services.AddHostedService<Catalog.PurchaseIntentReceiptCleanupWorker>();
        services.AddScoped<IPurchaseIntentStore, Catalog.PurchaseIntentStore>();
        services.AddScoped<IShowcaseQueries, Queries.ShowcaseQueries>();
        services.AddScoped<ICatalogCourseProjectionStore, Catalog.CatalogCourseProjectionStore>();
        services.AddScoped<ICatalogCourseEditStore, Catalog.CatalogCourseEditStore>();
        services.AddScoped<ICatalogOutboxMessageWriter, CatalogOutboxMessageWriter>();
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, CommerceUnitOfWork>();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IServiceAssertionReplayStore, ServiceAssertionReplayStore>();

        return services;
    }
}
