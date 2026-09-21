using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Health;
using CodeForCoders.Media.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Media.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<MediaDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                MediaSchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, MediaUnitOfWork>();
        services.AddOptions<AwsMediaOptions>()
            .Bind(configuration.GetSection(AwsMediaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Region), "AWS region is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName), "S3 bucket name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CloudFrontDistributionDomain), "CloudFront distribution domain is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ObjectKeyPrefix), "Media object key prefix is required.")
            .ValidateOnStart();
        services.AddSingleton<IMediaCdnPort, CloudFrontMediaCdnAdapter>();
        services.AddScoped<IMediaStoragePort, S3MediaStorageAdapter>();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();

        return services;
    }
}
