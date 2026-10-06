using CodeForCoders.Commerce.Infra.Messaging;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class OrderFixture(ShowcaseApiFactory factory, Guid tenant)
{
    public Guid Student { get; } = Guid.CreateVersion7();
    public ShowcaseApiFactory Factory => factory;
    public Guid Tenant => tenant;
    public static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    public AsyncServiceScope Scope()
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        return scope;
    }
    public async Task<Guid[]> SeedAsync()
    {
        await using var scope = Scope();
        var courseId = Guid.CreateVersion7();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(tenant, courseId, 1, rich: true, level: "beginner")), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.SingleAsync(item => item.CourseId == courseId, Cancellation);
        var now = DateTimeOffset.UtcNow;
        var monthly = course.CreateOffer(new("12 months", 49700, AccessPeriod.Create("months", 12)), now);
        var lifetime = course.CreateOffer(new("Lifetime", 89700, AccessPeriod.Create("lifetime", null)), now);
        course.PublishOffer(monthly.OfferId, now); course.PublishOffer(lifetime.OfferId, now);
        await db.SaveChangesAsync(Cancellation);
        return [courseId, monthly.OfferId, lifetime.OfferId];
    }
    public HttpClient Client(Guid? student = null, bool actor = false)
    {
        var client = factory.CreateClient();
        var claims = new List<Claim> { new("sub", (student ?? Student).ToString()), new("tenantId", tenant.ToString()), new("scope", "orders:use") };
        if (actor) claims.Add(new("permissions", "financeiro.ler"));
        var token = new JwtSecurityToken("identity", "commerce", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    public async Task<HttpResponseMessage> CreateAsync(Guid offer, string key, Guid? student = null)
    {
        using var client = Client(student);
        return await CreateAsync(client, offer, key);
    }
    public static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid offer, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/orders") { Content = JsonContent.Create(new { offerId = offer }) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Cancellation);
    }
    public async Task<JsonElement> SummaryAsync(Guid offer)
    {
        using var client = Client(); using var response = await client.GetAsync($"/internal/v1/offers/{offer}/purchase-summary", Cancellation);
        Assert.Equal(200, (int)response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await BodyAsync(response); OrderHttpContract.AssertValid(body, "PurchaseSummary"); return body;
    }
    public static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)); return json.RootElement.Clone(); }
}
