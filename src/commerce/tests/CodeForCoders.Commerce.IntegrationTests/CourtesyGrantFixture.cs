using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Json;
using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyGrantFixture : IAsyncDisposable
{
    public ConcurrentQueue<string> Logs { get; } = new();
    public ConcurrentQueue<string> Spans { get; } = new();
    private readonly ActivityListener listener;
    public Guid Tenant { get; } = Guid.CreateVersion7();
    public Guid Actor { get; } = Guid.CreateVersion7();
    public Guid Student { get; } = Guid.CreateVersion7();
    public Guid Course { get; } = Guid.CreateVersion7();
    public CourtesyIdentityConfirmationHandler Identity { get; } = new();
    public CourtesyGrantTestClock Clock { get; } = new();
    public CatalogCourseApiFactory Factory { get; }
    public HttpClient Client { get; }
    public CourtesyGrantFixture(CommerceIntegrationFixture infra, Action<IServiceCollection>? customize = null)
    {
        Factory = new(infra)
        {
            CustomizeServices = services =>
        {
            services.AddLogging(logging => logging.AddProvider(new CourtesyCapturedLogProvider(Logs)));
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<IStudentAccountConfirmationClient, StudentAccountConfirmationClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
            customize?.Invoke(services);
        }
        };
        listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Spans.Enqueue(activity.DisplayName + " " + string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))),
        };
        ActivitySource.AddActivityListener(listener);
        Client = Factory.CreateClient(); Authorize();
    }
    public void Authorize(Guid? tenant = null, string permission = "cortesia.conceder")
    {
        var token = new JwtSecurityToken("identity", "commerce", [new Claim("sub", Actor.ToString()),
            new Claim("tenantId", (tenant ?? Tenant).ToString()), new Claim("roles", "financeiro"), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(Factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        Client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
    public object Body(string reason = "Bolsa de mentoria", int months = 6) => new { studentId = Student, courseId = Course, accessPeriod = new { type = "months", months }, reason };
    public async Task SeedAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant);
        await scope.ServiceProvider.GetRequiredService<IEntitlementCourseProjectionStore>().ApplyAsync(
            CodeForCoders.Commerce.Infra.Messaging.PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(Tenant, Course, title: "Fundamentos de C#")), TestContext.Current.CancellationToken);
    }
    public Task<HttpResponseMessage> GrantAsync(object? body = null, string key = "courtesy-test-key")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/courtesy-grants") { Content = JsonContent.Create(body ?? Body()) };
        request.Headers.Add("Idempotency-Key", key); return Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    public async Task AssertEmptyAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.False(await db.AccessGrants.IgnoreQueryFilters().AnyAsync(item => item.TenantId == Tenant, TestContext.Current.CancellationToken));
        Assert.False(await db.Enrollments.IgnoreQueryFilters().AnyAsync(item => item.TenantId == Tenant, TestContext.Current.CancellationToken));
        Assert.False(await db.GrantReceipts.IgnoreQueryFilters().AnyAsync(item => item.TenantId == Tenant, TestContext.Current.CancellationToken));
        Assert.False(await db.EntitlementOutboxMessages.IgnoreQueryFilters().AnyAsync(item => item.TenantId == Tenant, TestContext.Current.CancellationToken));
    }
    public async ValueTask DisposeAsync() { Client.Dispose(); await Factory.DisposeAsync(); listener.Dispose(); }
}
