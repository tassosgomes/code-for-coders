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
using Microsoft.Extensions.Logging;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyBffApiFactory : WebApplicationFactory<Program>
{
    public CourtesyCoursesHttpHandler Courses { get; } = new();
    public CourseIdentityHandler Identity { get; } = new();
    public CourtesyIdentityHandler Lookup { get; } = new();
    public string PublicKey { get; private set; } = string.Empty;
    public System.Collections.Concurrent.ConcurrentQueue<string> Logs { get; } = new();
    public CourseBffSessionStore Sessions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("CourseAuthoring:Enabled", "true");
        builder.UseSetting("RabbitMq:Username", "test"); builder.UseSetting("RabbitMq:Password", "test");
        builder.UseSetting("StaffIdentity:BaseAddress", "http://identity.test/");
        builder.UseSetting("Learning:BaseAddress", "http://learning.test/");
        builder.UseSetting("Audit:BaseAddress", "http://audit.test/");
        builder.UseSetting("Commerce:BaseAddress", "http://commerce.test/");
        builder.UseSetting("Media:BaseAddress", "http://media.test/");
        builder.UseSetting("StaffIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        using var rsa = RSA.Create(2048);
        PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        builder.UseSetting("StaffIdentity:SigningKeyBase64", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.ConfigureLogging(logging => logging.AddProvider(new CourtesyCapturedLogProvider(Logs)));
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService)).ToList()) services.Remove(registration);
            services.RemoveAll<IBffSessionStore>(); services.AddSingleton<IBffSessionStore>(Sessions);
            services.AddHttpClient<IStudentAccountIdentityClient, StudentAccountIdentityClient>().ConfigurePrimaryHttpMessageHandler(() => Lookup);
            services.AddHttpClient<IStaffSessionIdentityClient, StaffSessionIdentityClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            services.AddHttpClient<ICourtesyCoursesClient, CourtesyCoursesClient>().ConfigurePrimaryHttpMessageHandler(() => Courses);
            // Keep the production typed client registration and resilience pipeline; replace only the HTTP boundary.
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
