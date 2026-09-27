using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;
using CodeForCoders.BffAdmin.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class BffAdminApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public StaffPasswordResetIdentityHandler IdentityHandler { get; } = new();

    public StaffSessionIdentityHandler StaffSessionIdentityHandler { get; } = new();

    public StaffInvitationIdentityHandler StaffInvitationIdentityHandler { get; } = new();

    public StaffMemberIdentityHandler StaffMemberIdentityHandler { get; } = new();

    public CommerceFinanceAreaHandler CommerceFinanceAreaHandler { get; } = new();

    public VideoLibraryHandler VideoLibraryHandler { get; } = new();

    public VideoUploadHandler VideoUploadHandler { get; } = new();

    public AuditRecordSearchHandler AuditRecordSearchHandler { get; } = new();

    public AuditIdentityReferenceHandler AuditIdentityReferenceHandler { get; } = new();

    public InMemoryBffSessionStore SessionStore { get; } = new();

    public CapturedLogProvider CapturedLogs { get; } = new();

    public string IdentityPublicKeyBase64 { get; private set; } = string.Empty;

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_bff_admin")
        .WithUsername("code_for_coders_bff_admin")
        .WithPassword("code_for_coders_bff_admin")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new BffAdminDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", PostgreSql.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StaffIdentity:BaseAddress", "http://identity.test/");
        builder.UseSetting("StaffIdentity:Issuer", "bff-admin");
        builder.UseSetting("StaffIdentity:Audience", "identity-internal");
        builder.UseSetting("StaffIdentity:SigningKeyId", "e2e-test");
        builder.UseSetting("StaffIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
        builder.UseSetting("OutboxProtection:KeyVersion", "e2e-v1");
        builder.UseSetting("Commerce:BaseAddress", "http://commerce.test/");
        builder.UseSetting("Media:BaseAddress", "http://media.test/");
        builder.UseSetting("Audit:BaseAddress", "http://audit.test/");
        builder.UseSetting("BffSecurity:AllowedOrigins:0", "http://localhost:8081");
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        IdentityPublicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        builder.UseSetting("StaffIdentity:SigningKeyBase64", Convert.ToBase64String(privateKey));
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(CapturedLogs);
            logging.AddFilter<CapturedLogProvider>(null, LogLevel.Trace);
        });
        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }

            services.AddSingleton(IdentityHandler);
            services.AddSingleton(StaffSessionIdentityHandler);
            services.AddSingleton(StaffInvitationIdentityHandler);
            services.AddSingleton(StaffMemberIdentityHandler);
            services.AddSingleton(CommerceFinanceAreaHandler);
            services.AddSingleton(VideoLibraryHandler);
            services.AddSingleton(VideoUploadHandler);
            services.AddSingleton(AuditRecordSearchHandler);
            services.AddSingleton(AuditIdentityReferenceHandler);
            services.RemoveAll<IBffSessionStore>();
            services.AddSingleton<IBffSessionStore>(SessionStore);
            services.AddHttpClient<IStaffPasswordResetIdentityClient, StaffPasswordResetIdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StaffPasswordResetIdentityHandler>());
            services.AddHttpClient<IStaffSessionIdentityClient, StaffSessionIdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StaffSessionIdentityHandler>());
            services.RemoveAll<IAuditRecordClient>();
            services.AddHttpClient<IAuditRecordClient, AuditRecordClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<AuditRecordSearchHandler>());
            services.RemoveAll<IAuditIdentityReferenceClient>();
            services.AddHttpClient<IAuditIdentityReferenceClient, AuditIdentityReferenceClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<AuditIdentityReferenceHandler>());
            services.AddHttpClient<IStaffInvitationIdentityClient, StaffInvitationIdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StaffInvitationIdentityHandler>());
            services.AddHttpClient<IStaffMemberIdentityClient, StaffMemberIdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StaffMemberIdentityHandler>());
            services.RemoveAll<ICommerceFinanceAreaClient>();
            services.AddHttpClient<ICommerceFinanceAreaClient, CommerceFinanceAreaClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<CommerceFinanceAreaHandler>());
            services.RemoveAll<IVideoLibraryClient>();
            services.AddHttpClient<IVideoLibraryClient, VideoLibraryClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<VideoLibraryHandler>());
            services.RemoveAll<IVideoUploadClient>();
            services.AddHttpClient<IVideoUploadClient, VideoUploadClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<VideoUploadHandler>());
        });
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class BffAdminApiCollection : ICollectionFixture<BffAdminApiFactory>
{
    public const string Name = "bff-admin-e2e";
}
