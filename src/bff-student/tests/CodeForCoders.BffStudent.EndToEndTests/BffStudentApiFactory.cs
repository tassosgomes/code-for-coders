using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Application.Common;
using System.Security.Cryptography;
using CodeForCoders.BffStudent.Infra.Data;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

public sealed class BffStudentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly RSA SigningKey = RSA.Create(2048);

    public StudentRegistrationIdentityClientStub StudentRegistrationClient { get; } = new();

    public StudentPasswordRecoveryIdentityClientStub StudentPasswordRecoveryClient { get; } = new();

    public StudentPasswordChangeIdentityClientStub StudentPasswordChangeClient { get; } = new();

    public StudentSessionIdentityClientStub StudentSessionClient { get; } = new();

    public InMemoryBffSessionStore SessionStore { get; } = new();

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_bff_student")
        .WithUsername("code_for_coders_bff_student")
        .WithPassword("code_for_coders_bff_student")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<BffStudentDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new BffStudentDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", PostgreSql.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StudentIdentity:BaseAddress", "http://identity.integration.test/");
        builder.UseSetting("StudentIdentity:SigningKeyId", "test-key");
        builder.UseSetting("StudentIdentity:SigningKeyBase64", Convert.ToBase64String(SigningKey.ExportPkcs8PrivateKey()));
        builder.UseSetting("StudentIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStudentRegistrationIdentityClient>();
            services.AddSingleton<IStudentRegistrationIdentityClient>(StudentRegistrationClient);
            services.RemoveAll<IStudentPasswordRecoveryIdentityClient>();
            services.AddSingleton<IStudentPasswordRecoveryIdentityClient>(StudentPasswordRecoveryClient);
            services.RemoveAll<IStudentPasswordChangeIdentityClient>();
            services.AddSingleton<IStudentPasswordChangeIdentityClient>(StudentPasswordChangeClient);
            services.RemoveAll<IStudentSessionIdentityClient>();
            services.AddSingleton<IStudentSessionIdentityClient>(StudentSessionClient);
            services.RemoveAll<IBffSessionStore>();
            services.AddSingleton<IBffSessionStore>(SessionStore);
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }
        });
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class BffStudentApiCollection : ICollectionFixture<BffStudentApiFactory>
{
    public const string Name = "bff-student-e2e";
}
