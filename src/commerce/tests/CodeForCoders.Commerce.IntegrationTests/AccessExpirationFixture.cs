using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using CodeForCoders.Commerce.Infra.Data.Entitlement;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class AccessExpirationFixture : IAsyncDisposable
{
    private readonly MeterListener metrics = new();
    public CatalogCourseApiFactory Factory { get; }
    public CourtesyGrantTestClock Clock { get; } = new();
    public Guid Tenant { get; } = Guid.CreateVersion7();
    public Guid Student { get; } = Guid.CreateVersion7();
    public Guid Course { get; } = Guid.CreateVersion7();
    public ConcurrentQueue<string> Logs { get; } = new();
    public ConcurrentQueue<(double Seconds, int Tags)> Measurements { get; } = new();
    public string Queue { get; }
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private AccessExpirationFixture(CommerceIntegrationFixture infra, string connectionString, bool delivery,
        Action<IServiceCollection>? customize)
    {
        var suffix = Guid.CreateVersion7().ToString("N");
        Queue = "commerce.expiration-retention-" + suffix;
        Factory = new(infra)
        {
            DatabaseConnectionString = connectionString,
            CustomizeServices = services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddLogging(logging => logging.AddProvider(new CourtesyCapturedLogProvider(Logs)));
                services.Configure<AccessExpirationOptions>(options =>
                {
                    options.Enabled = delivery;
                    options.PollingIntervalSeconds = 1;
                });
                services.Configure<RabbitMqOptions>(options =>
                {
                    options.Exchange = "commerce.expiration-" + suffix;
                    options.EntitlementFactRetentionQueue = Queue;
                    options.OfferRetentionQueue = "commerce.expiration-offers-" + suffix;
                    options.HeartbeatQueue = "commerce.expiration-heartbeat-" + suffix;
                });
                services.Configure<OutboxOptions>(options => options.PollingIntervalSeconds = 1);
                if (!delivery)
                {
                    var publisher = services.Single(item => item.ServiceType == typeof(IHostedService)
                        && item.ImplementationType == typeof(OutboxPublisherWorker));
                    services.Remove(publisher);
                }
                customize?.Invoke(services);
            }
        };
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "commerce.entitlement.expiration.lag") listener.EnableMeasurementEvents(instrument);
        };
        metrics.SetMeasurementEventCallback<double>((_, value, tags, _) => Measurements.Enqueue((value, tags.Length)));
        metrics.Start();
    }

    public static async Task<AccessExpirationFixture> CreateAsync(CommerceIntegrationFixture infra,
        bool delivery = false, Action<IServiceCollection>? customize = null)
    {
        var connection = new NpgsqlConnectionStringBuilder(infra.PostgreSql.GetConnectionString());
        var database = "expiration_" + Guid.CreateVersion7().ToString("N");
        await using (var admin = new NpgsqlConnection(connection.ConnectionString))
        {
            await admin.OpenAsync(Cancellation);
            await using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE {database}";
            await command.ExecuteNonQueryAsync(Cancellation);
        }
        connection.Database = database;
        await using (var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connection.ConnectionString).Options, new TenantContext()))
            await db.Database.MigrateAsync(Cancellation);
        var fixture = new AccessExpirationFixture(infra, connection.ConnectionString, delivery, customize);
        using var client = fixture.Factory.CreateClient();
        return fixture;
    }

    public AsyncServiceScope Scope()
    {
        var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant);
        return scope;
    }

    public async Task<AccessGrant> SeedAsync(DateTimeOffset grantedAt, string period = "months", Guid? tenant = null)
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var enrollment = await db.Enrollments.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.TenantId == (tenant ?? Tenant), Cancellation);
        if (enrollment is null)
        {
            enrollment = Enrollment.Create(tenant ?? Tenant, Student, Course, grantedAt);
            db.Enrollments.Add(enrollment);
        }
        var grant = AccessGrant.CreateCourtesy(enrollment,
            new(Guid.CreateVersion7(), "Expiration proof", period, period == "months" ? 1 : null, grantedAt), TimeZoneInfo.Utc);
        db.AccessGrants.Add(grant);
        await db.SaveChangesAsync(Cancellation);
        return grant;
    }

    public async Task<int> CycleAsync()
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<AccessExpirationCycle>().RunAsync(Cancellation);
    }

    public async Task<EntitlementOutboxMessage[]> FactsAsync()
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().EntitlementOutboxMessages
            .IgnoreQueryFilters().AsNoTracking().OrderBy(item => item.OccurredOn).ToArrayAsync(Cancellation);
    }

    public async Task<AccessGrant> ReloadAsync(Guid id)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.IgnoreQueryFilters()
            .AsNoTracking().SingleAsync(item => item.Id == id, Cancellation);
    }

    public async Task<DecideAccessOutput> DecideAsync()
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<IDecideAccess>().ExecuteAsync(new(Student, Course), Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        metrics.Dispose();
        await Factory.DisposeAsync();
    }
}
