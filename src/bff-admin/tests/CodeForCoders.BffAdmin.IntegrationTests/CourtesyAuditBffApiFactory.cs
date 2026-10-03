using System.Security.Cryptography;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyAuditBffApiFactory : WebApplicationFactory<Program>
{
    public CourtesyAuditIdentityHandler Identity { get; } = new();
    public CourtesyAuditHandler Audit { get; } = new();
    public CourseLearningHandler Learning { get; } = new();
    private readonly CourseBffSessionStore sessions = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("RabbitMq:Username", "test");
        builder.UseSetting("RabbitMq:Password", "test");
        builder.UseSetting("StaffIdentity:BaseAddress", "http://identity.test/");
        builder.UseSetting("Learning:BaseAddress", "http://learning.test/");
        builder.UseSetting("Audit:BaseAddress", "http://audit.test/");
        builder.UseSetting("StaffIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        using var rsa = RSA.Create(2048);
        builder.UseSetting("StaffIdentity:SigningKeyBase64", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Audit.CourseId = Learning.CourseId;
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService)).ToArray()) services.Remove(registration);
            services.RemoveAll<IBffSessionStore>();
            services.AddSingleton<IBffSessionStore>(sessions);
            // Exercise the production clients, authentication and resilience; control only the HTTP boundary.
            services.AddHttpClient<IStaffSessionIdentityClient, StaffSessionIdentityClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            services.AddHttpClient<IAuditIdentityReferenceClient, AuditIdentityReferenceClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            services.AddHttpClient<ICourseAuthoringClient, CourseAuthoringClient>().ConfigurePrimaryHttpMessageHandler(() => Learning);
            services.AddHttpClient<IAuditRecordClient, AuditRecordClient>().ConfigurePrimaryHttpMessageHandler(() => Audit);
        });
    }

    public async Task<HttpClient> AuthenticatedAsync()
    {
        var session = new OpaqueBffSession("opaque-audit-session", Guid.CreateVersion7(), "csrf-audit", DateTimeOffset.UtcNow.AddHours(1));
        Identity.ExpectedSessionId = session.IdentitySessionId;
        await sessions.StoreAsync(session, Xunit.TestContext.Current.CancellationToken);
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "staff_session=opaque-audit-session");
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:8081");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf-audit");
        return client;
    }
}
