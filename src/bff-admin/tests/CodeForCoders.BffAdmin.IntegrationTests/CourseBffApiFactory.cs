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

public sealed class CourseBffApiFactory : WebApplicationFactory<Program>
{
    public CourseIdentityHandler Identity { get; } = new();
    public CourseLearningHandler Learning { get; } = new();
    public CourseBffSessionStore Sessions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("RabbitMq:Username", "test"); builder.UseSetting("RabbitMq:Password", "test");
        builder.UseSetting("StaffIdentity:BaseAddress", "http://identity.test/");
        builder.UseSetting("Learning:BaseAddress", "http://learning.test/");
        builder.UseSetting("StaffIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        using var rsa = RSA.Create(2048);
        builder.UseSetting("StaffIdentity:SigningKeyBase64", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService)).ToList()) services.Remove(registration);
            services.RemoveAll<IBffSessionStore>(); services.AddSingleton<IBffSessionStore>(Sessions);
            services.AddHttpClient<IStaffSessionIdentityClient, StaffSessionIdentityClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            // Keep the production typed client registration and resilience pipeline; replace only the HTTP boundary.
            services.AddHttpClient<ICourseAuthoringClient, CourseAuthoringClient>().ConfigurePrimaryHttpMessageHandler(() => Learning);
        });
    }

    public async Task<HttpClient> AuthenticatedAsync()
    {
        var session = new OpaqueBffSession("opaque-course-session", Guid.CreateVersion7(), "csrf-course", DateTimeOffset.UtcNow.AddHours(1));
        await Sessions.StoreAsync(session, Xunit.TestContext.Current.CancellationToken);
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "staff_session=opaque-course-session");
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:8081");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "csrf-course");
        return client;
    }
}
