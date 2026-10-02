using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Api.Extensions;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Services;
using CodeForCoders.Identity.Application.UseCases.Accounts.AuthenticateStaffSession;
using CodeForCoders.Identity.Application.UseCases.Accounts.RegisterStudentAccount;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using CodeForCoders.Identity.Infra.Data.Accounts;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class StudentAccountLookupFixture : IAsyncLifetime
{
    public Guid TenantId { get; } = Guid.CreateVersion7();
    public Guid OtherTenantId { get; } = Guid.CreateVersion7();
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18").Build();
    private readonly IContainer valkey = new ContainerBuilder("valkey/valkey:8-alpine")
        .WithPortBinding(6379, true).WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    public WebApplication App { get; private set; } = null!;
    public ConcurrentQueue<string> Logs { get; } = new();
    public ConcurrentQueue<string> Spans { get; } = new();
    private ActivityListener? listener;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), valkey.StartAsync());
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src/identity/CodeForCoders.Identity.slnx"))) directory = directory.Parent;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.Combine(directory!.FullName, "src/identity/src/CodeForCoders.Identity.Api"),
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
            ["Valkey:ConnectionString"] = $"{valkey.Hostname}:{valkey.GetMappedPublicPort(6379)},abortConnect=false",
            ["RabbitMq:Username"] = "test",
            ["RabbitMq:Password"] = "test",
            ["Idempotency:FingerprintKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["OutboxProtection:KeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["ServiceAssertions:Issuers:bff-student:PublicKeys:test"] = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()),
            ["ServiceAssertions:Issuers:bff-student:AllowedTenantIds:0"] = TenantId.ToString(),
            ["ServiceAssertions:Issuers:bff-admin:PublicKeys:test"] = Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()),
            ["ServiceAssertions:Issuers:bff-admin:AllowedTenantIds:0"] = TenantId.ToString(),
            ["ServiceAssertions:Issuers:bff-admin:AllowedTenantIds:1"] = OtherTenantId.ToString(),
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Information",
            ["StaffSessionTokens:SigningKeyId"] = "test",
            ["StaffSessionTokens:SigningKeyBase64"] = Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey()),
            ["StaffSessionTokens:AudienceScopes:commerce"] = "finance-area:read",
        });
        builder.AddIdentityConfiguration();
        builder.Logging.AddProvider(new LookupCapturedLogProvider(Logs));
        foreach (var item in builder.Services.Where(item => item.ServiceType == typeof(IHostedService)).ToArray()) builder.Services.Remove(item);
        App = builder.Build();
        await using (var scope = App.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        App.UseApplicationPipeline();
        App.MapApiEndpoints();
        listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Spans.Enqueue(activity.DisplayName + " " + string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                + " " + string.Join(" ", activity.Events.Select(evt => evt.Name + string.Join(" ", evt.Tags.Select(tag => $"{tag.Key}={tag.Value}"))))),
        };
        ActivitySource.AddActivityListener(listener);
        await App.StartAsync();
    }

    public async Task<Guid> RegisterAsync(string email, Guid? tenantId = null)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId ?? TenantId);
        await scope.ServiceProvider.GetRequiredService<IRegisterStudentAccount>().ExecuteAsync(new RegisterStudentAccountInput(
            tenantId ?? TenantId, "Lookup Student", email, "SenhaForte1!", Guid.CreateVersion7().ToString()), TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.Accounts.IgnoreQueryFilters().Where(account => account.TenantId == (tenantId ?? TenantId) && account.NormalizedEmail == email.Trim().ToLowerInvariant() && account.DeactivatedOn == null)
            .Select(account => account.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    public async Task<Guid> LoginAsync(string? role, bool student = false)
    {
        var email = $"{Guid.CreateVersion7()}@staff.test";
        var now = TimeProvider.System.GetUtcNow();
        var accountId = Guid.CreateVersion7();
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Accounts.Add(student ? Account.CreateStudent(accountId, TenantId, "Staff Account", email, email)
            : Account.CreateInternal(accountId, TenantId, "Staff Account", email, email));
        db.Credentials.Add(Credential.Create(Guid.CreateVersion7(), TenantId, accountId, new Pbkdf2PasswordHasher().Hash("SenhaForte1!"), now));
        if (role is not null) db.StaffRoleAssignments.Add(StaffRoleAssignment.Create(Guid.CreateVersion7(), TenantId, accountId, role, now));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        if (student)
        {
            var id = Guid.CreateVersion7();
            db.StaffSessions.Add(StaffSession.Create(id, TenantId, accountId, now, now.AddHours(1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return id;
        }
        var session = await scope.ServiceProvider.GetRequiredService<IAuthenticateStaffSession>().ExecuteAsync(
            new AuthenticateStaffSessionInput(TenantId, email, "SenhaForte1!", Guid.CreateVersion7().ToString()), TestContext.Current.CancellationToken);
        return session.SessionId;
    }

    public string Assertion(string scope = "student-account:lookup", Guid? tenantId = null, RSA? key = null, string issuer = "bff-admin")
    {
        var now = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = "test" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer,
            sub = issuer,
            aud = "identity-internal",
            tenantId = tenantId ?? TenantId,
            scope,
            jti = Guid.CreateVersion7(),
            iat = now,
            nbf = now - 5,
            exp = now + 30
        }));
        return $"{header}.{payload}.{Encode((key ?? signingKey).SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))}";
    }

    public async Task<HttpResponseMessage> LookupAsync(Guid? sessionId, object body, string? assertion)
    {
        using var client = App.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/student-account-lookups") { Content = JsonContent.Create(body) };
        if (assertion is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        if (sessionId is not null) request.Headers.Add("X-Staff-Session", sessionId.ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async Task<string> SessionTokenAsync(Guid sessionId)
    {
        using var client = App.GetTestClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/staff-session-validations")
        {
            Content = JsonContent.Create(new { sessionId, audience = "commerce" }),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Assertion("staff-sessions:validate"));
        using var response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("cortesia.conceder", document.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async ValueTask DisposeAsync()
    {
        listener?.Dispose();
        if (App is not null) await App.DisposeAsync();
        signingKey.Dispose();
        await valkey.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
