using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Infra.Data.Adapters;
using CodeForCoders.Media.Infra.Data.Configuration;
using CodeForCoders.Media.Infra.Data.Health;
using CodeForCoders.Media.Infra.Data.Idempotency;
using CodeForCoders.Media.Infra.Data.Outbox;
using CodeForCoders.Media.Infra.Data.Videos;
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
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, MediaUnitOfWork>();
        services.AddScoped<IVideoQueries, VideoQueries>();
        services.AddScoped<IVideoUploadRepository, VideoUploadRepository>();
        services.AddScoped<IVideoPreparationRepository, VideoPreparationRepository>();
        services.AddScoped<IOperationIdempotencyRepository, OperationIdempotencyRepository>();
        services.AddOptions<AwsMediaOptions>()
            .Bind(configuration.GetSection(AwsMediaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Region), "AWS region is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName), "S3 bucket name is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ObjectKeyPrefix), "Media object key prefix is required.")
            .Validate(options => string.IsNullOrWhiteSpace(options.EndpointInternal) == string.IsNullOrWhiteSpace(options.EndpointPublic), "Both S3 endpoints must be configured together.")
            .Validate(options => IsHttpEndpoint(options.EndpointInternal) && IsHttpEndpoint(options.EndpointPublic), "S3 endpoints must be absolute HTTP(S) URLs.")
            .Validate(options => string.IsNullOrWhiteSpace(options.AccessKeyId) == string.IsNullOrWhiteSpace(options.SecretAccessKey), "S3 access key and secret must be configured together.")
            .ValidateOnStart();
        services.AddSingleton<S3MediaClientPair>();
        services.AddScoped<IMediaStoragePort, S3MediaStorageAdapter>();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();

        return services;
    }

    private static bool IsHttpEndpoint(string? value)
        => string.IsNullOrWhiteSpace(value)
            || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
