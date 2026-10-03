using CodeForCoders.BffAdmin.Application;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Infra.Data;
using CodeForCoders.BffAdmin.Infra.Messaging;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddBffAdminConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddScoped<CourseVideoEnricher>();
        builder.Services.AddScoped<CourseAuditReferenceEnricher>();
        builder.Services.AddScoped<OfferAuditReferenceEnricher>();
        builder.Services.AddOptions<LearningApiOptions>()
            .Bind(builder.Configuration.GetSection(LearningApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https" && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Learning base address must be an absolute HTTP(S) URL ending in a slash.").ValidateOnStart();
        builder.Services.AddHttpClient<ICourseAuthoringClient, CourseAuthoringClient>((services, client) =>
            client.BaseAddress = new Uri(services.GetRequiredService<IOptions<LearningApiOptions>>().Value.BaseAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddBffProxyConfiguration(builder.Configuration);
        builder.AddStaffIdentityConfiguration();
        builder.Services.AddOptions<AuditApiOptions>()
            .Bind(builder.Configuration.GetSection(AuditApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Audit base address must be an absolute HTTP(S) URL ending in a slash.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<IAuditRecordClient, AuditRecordClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<AuditApiOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddOptions<CommerceApiOptions>()
            .Bind(builder.Configuration.GetSection(CommerceApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Commerce base address must be an absolute HTTP(S) URL ending in a slash.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<ICommerceFinanceAreaClient, CommerceFinanceAreaClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<CommerceApiOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient<ICommerceCatalogClient, CommerceCatalogClient>((services, client) =>
                client.BaseAddress = new Uri(services.GetRequiredService<IOptions<CommerceApiOptions>>().Value.BaseAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient<ICourtesyGrantsClient, CourtesyGrantsClient>((services, client) =>
                client.BaseAddress = new Uri(services.GetRequiredService<IOptions<CommerceApiOptions>>().Value.BaseAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient<ICourtesyCoursesClient, CourtesyCoursesClient>((services, client) =>
                client.BaseAddress = new Uri(services.GetRequiredService<IOptions<CommerceApiOptions>>().Value.BaseAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient<IOfferReferenceClient, OfferReferenceClient>((services, client) =>
                client.BaseAddress = new Uri(services.GetRequiredService<IOptions<CommerceApiOptions>>().Value.BaseAddress))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddOptions<MediaApiOptions>()
            .Bind(builder.Configuration.GetSection(MediaApiOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && options.BaseAddress.EndsWith("/", StringComparison.Ordinal),
                "Media base address must be an absolute HTTP(S) URL ending in a slash.")
            .ValidateOnStart();
        builder.Services.AddHttpClient<IVideoLibraryClient, VideoLibraryClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<MediaApiOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient<IVideoUploadClient, VideoUploadClient>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<MediaApiOptions>>().Value;
                client.BaseAddress = new Uri(settings.BaseAddress, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        builder.Services.AddHttpClient("bff-admin-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
