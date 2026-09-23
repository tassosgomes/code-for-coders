using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data.Accounts;
using CodeForCoders.Identity.Infra.Data.Configuration;
using CodeForCoders.Identity.Infra.Data.Health;
using CodeForCoders.Identity.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Identity.Infra.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                "__ef_migrations_history",
                IdentitySchema.Name));
            if (environment.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });
        services.AddScoped<IOutboxMessageWriter, OutboxMessageWriter>();
        services.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IIdentityRegistrationStore, IdentityRegistrationStore>();
        services.AddScoped<IIdentityConfirmationStore, IdentityConfirmationStore>();
        services.AddScoped<IIdentitySessionStore, IdentitySessionStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IServiceAssertionReplayStore, ServiceAssertionReplayStore>();
        services.AddOptions<RegistrationOptions>()
            .Bind(configuration.GetSection(RegistrationOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.ConfirmationBaseUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https", "Student account confirmation URL must be absolute HTTP(S).")
            .Validate(options => options.ConfirmationLifetimeHours > 0, "Student account confirmation lifetime must be positive.")
            .ValidateOnStart();
        services.AddOptions<StudentSessionOptions>()
            .Bind(configuration.GetSection(StudentSessionOptions.SectionName))
            .Validate(options => options.InactivityTimeoutMinutes is >= 1 and <= 1440,
                "Student session inactivity timeout must be between 1 and 1440 minutes.")
            .ValidateOnStart();
        services.AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .Validate(options => IsStrongKey(options.FingerprintKeyBase64), "Idempotency fingerprint key must contain at least 256 bits.")
            .ValidateOnStart();
        services.AddOptions<OutboxDestinationOptions>()
            .Bind(configuration.GetSection(OutboxDestinationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Exchange), "Identity exchange is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.NotificationExchange), "Notification exchange is required.")
            .ValidateOnStart();
        services.AddOptions<ValkeyOptions>()
            .Bind(configuration.GetSection(ValkeyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Valkey connection string is required.")
            .ValidateOnStart();
        services.AddSingleton<ValkeyConnectionProvider>();

        return services;
    }

    private static bool IsStrongKey(string base64Key)
    {
        try
        {
            return Convert.FromBase64String(base64Key).Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
