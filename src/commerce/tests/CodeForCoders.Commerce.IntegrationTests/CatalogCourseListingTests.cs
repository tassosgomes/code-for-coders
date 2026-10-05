using Microsoft.Extensions.DependencyInjection.Extensions;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class CatalogCourseListingTests(CommerceIntegrationFixture fixture, CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(RealHostStartsWithConsumerAndListsOnlyPublishedCourses))]
    public async Task RealHostStartsWithConsumerAndListsOnlyPublishedCourses()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<CatalogCoursePage>(Cancellation);
        Assert.Empty(page!.Data); Assert.Equal(0, page.Pagination.Total);
        Assert.Contains(factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), service => service is CatalogCourseConsumerWorker);
    }

    [Theory(DisplayName = nameof(OtherRolesCannotListTheCatalog))]
    [InlineData("administrador", "acesso.gerir")]
    [InlineData("professor", "autoria.editar")]
    [InlineData("suporte", "suporte.atender")]
    public async Task OtherRolesCannotListTheCatalog(string role, string permission)
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, Guid.CreateVersion7(), permission, role);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("PERMISSION_DENIED", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingAndInvalidTokensReturn401))]
    public async Task MissingAndInvalidTokensReturn401()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        using var missing = await client.GetAsync("/internal/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        using var invalid = await client.GetAsync("/internal/v1/catalog/courses", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    [Fact(DisplayName = nameof(CatalogIsIsolatedByTenantAndOrdersAndPaginatesByTitle))]
    public async Task CatalogIsIsolatedByTenantAndOrdersAndPaginatesByTitle()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        var tenant = Guid.CreateVersion7(); var other = Guid.CreateVersion7();
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(tenant, Guid.CreateVersion7(), title: "Zebra"));
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(tenant, Guid.CreateVersion7(), rich: true, title: "Alpha"));
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(other, Guid.CreateVersion7(), title: "Other school"));
        using var first = await SendAsync(factory, client, tenant, path: "?_page=1&_size=1");
        var page = await first.Content.ReadFromJsonAsync<CatalogCoursePage>(Cancellation);
        Assert.Equal("Alpha", Assert.Single(page!.Data).Title); Assert.Equal(2, page.Pagination.Total); Assert.Equal(2, page.Pagination.TotalPages);
        Assert.Equal(new CatalogOfferCounts(0, 0, 0), page.Data[0].OfferCounts); Assert.False(page.Data[0].InShowcase);
        using var second = await SendAsync(factory, client, tenant, path: "?_page=2&_size=1");
        Assert.Equal("Zebra", Assert.Single((await second.Content.ReadFromJsonAsync<CatalogCoursePage>(Cancellation))!.Data).Title);
        using var isolated = await SendAsync(factory, client, other);
        Assert.Equal("Other school", Assert.Single((await isolated.Content.ReadFromJsonAsync<CatalogCoursePage>(Cancellation))!.Data).Title);
    }

    [Fact(DisplayName = nameof(BrokerDeliveryIsMonotonicAndUpgradesLegacyFormatWithoutVideoIds))]
    public async Task BrokerDeliveryIsMonotonicAndUpgradesLegacyFormatWithoutVideoIds()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        var tenant = Guid.CreateVersion7(); var courseId = Guid.CreateVersion7();
        await PublishAsync(factory, CatalogCourseFactFixture.Create(tenant, courseId, 2));
        var legacy = await WaitForAsync(factory, tenant, courseId, course => course.VersionNumber == 2);
        Assert.Null(legacy.Level); Assert.Equal("", legacy.Description); Assert.Equal("1.0.0", legacy.SourceFormat);
        await PublishAsync(factory, CatalogCourseFactFixture.Create(tenant, courseId, 2, true));
        var rich = await WaitForAsync(factory, tenant, courseId, course => course.SourceFormat == "1.1.0");
        Assert.Equal("beginner", rich.Level); Assert.Equal("Pedagogical description", rich.Description);
        Assert.DoesNotContain("videoId", rich.StructureJson); Assert.Contains("Lesson", rich.StructureJson);
        await PublishAsync(factory, CatalogCourseFactFixture.Create(tenant, courseId, 2, true, "Duplicate"));
        await PublishAsync(factory, CatalogCourseFactFixture.Create(tenant, courseId, 1, true, "Older"));
        await WaitUntilQueueEmptyAsync(factory);
        var unchanged = await WaitForAsync(factory, tenant, courseId, course => course.VersionNumber == 2);
        Assert.Equal("Published course", unchanged.Title);
    }

    [Theory(DisplayName = nameof(PermanentErrorsGoToDeadLetterWithoutRetry))]
    [InlineData("invalid json")]
    [InlineData("{\"courseId\":\"00000000-0000-7000-8000-000000000001\"}")]
    public async Task PermanentErrorsGoToDeadLetterWithoutRetry(string payload)
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        await channel.QueuePurgeAsync("commerce.catalog-course.dlq", Cancellation);
        var body = Encoding.UTF8.GetBytes(payload); await PublishAsync(factory, body);
        BasicGetResult? delivery = null;
        for (var attempt = 0; attempt < 100 && delivery is null; attempt++)
        {
            delivery = await channel.BasicGetAsync("commerce.catalog-course.dlq", true, Cancellation);
            if (delivery is null) await Task.Delay(50, Cancellation);
        }
        Assert.NotNull(delivery); Assert.Equal(body, delivery.Body.ToArray());
        Assert.False(delivery.BasicProperties.Headers?.ContainsKey("x-delivery-count") == true);
        Assert.Null(await channel.BasicGetAsync("commerce.catalog-course.dlq", true, Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentFirstInsertAndUpdatesKeepTheGreatestVersion))]
    public async Task ConcurrentFirstInsertAndUpdatesKeepTheGreatestVersion()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        var tenant = Guid.CreateVersion7(); var course = Guid.CreateVersion7();
        await Task.WhenAll(Enumerable.Range(1, 8).Select(version => ApplyAsync(factory, CatalogCourseFactFixture.Create(tenant, course, version, true))));
        var result = await WaitForAsync(factory, tenant, course, row => row.VersionNumber == 8);
        Assert.Equal(8, result.VersionNumber);
    }

    [Fact(DisplayName = nameof(NewerLegacyPublicationClearsLevelAndDescription))]
    public async Task NewerLegacyPublicationClearsLevelAndDescription()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        var tenant = Guid.CreateVersion7(); var course = Guid.CreateVersion7();
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(tenant, course, rich: true));
        await PublishAsync(factory, CatalogCourseFactFixture.Create(tenant, course, 2));
        var result = await WaitForAsync(factory, tenant, course, row => row.VersionNumber == 2);
        Assert.Null(result.Level); Assert.Equal("", result.Description); Assert.Null(result.InShowcaseSince);
    }

    [Fact(DisplayName = nameof(TransientFailuresAreNotAcknowledgedAndExhaustTheirDeliveryLimit))]
    public async Task TransientFailuresAreNotAcknowledgedAndExhaustTheirDeliveryLimit()
    {
        var unavailable = new UnavailableCatalogProjectionStore();
        await using var factory = new CatalogCourseApiFactory(fixture)
        {
            CustomizeServices = services =>
            {
                services.RemoveAll<ICatalogCourseProjectionStore>();
                services.AddSingleton<ICatalogCourseProjectionStore>(unavailable);
                services.Configure<RabbitMqOptions>(options =>
                {
                    options.HeartbeatQueue = "commerce.platform-heartbeat-transient-test";
                    options.CatalogCourseQueue = "commerce.catalog-transient-test";
                    options.EntitlementCourseQueue = "commerce.catalog-test-entitlement-transient";
                    options.DeliveryLimit = 3;
                });
            },
        };
        using var client = factory.CreateClient();
        var body = CatalogCourseFactFixture.Create(Guid.CreateVersion7(), Guid.CreateVersion7());
        await PublishAsync(factory, body);
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        BasicGetResult? deadLetter = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (deadLetter is null && DateTimeOffset.UtcNow < deadline)
        {
            deadLetter = await channel.BasicGetAsync("commerce.catalog-transient-test.dlq", true, Cancellation);
            if (deadLetter is null) await Task.Delay(50, Cancellation);
        }
        Assert.NotNull(deadLetter);
        Assert.Equal(body, deadLetter.Body.ToArray());
        Assert.Equal(3, unavailable.Calls);
    }

    [Fact(DisplayName = nameof(InvalidPaginationReturnsContractProblem))]
    public async Task InvalidPaginationReturnsContractProblem()
    {
        var factory = hosts.CatalogWithWorkers; using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, Guid.CreateVersion7(), path: "?_page=0&_size=100");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_REQUEST", await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task ApplyAsync(CatalogCourseApiFactory factory, byte[] body)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(PublishedCourseFact.Parse(body), Cancellation);
    }

    private static async Task PublishAsync(CatalogCourseApiFactory factory, byte[] body)
    {
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        await channel.BasicPublishAsync("learning.events", PublishedCourseFact.RoutingKey, true,
            new BasicProperties { ContentType = "application/json", Persistent = true }, body, Cancellation);
    }

    private static async Task<CodeForCoders.Commerce.Domain.Entities.CatalogCourseView> WaitForAsync(
        CatalogCourseApiFactory factory, Guid tenantId, Guid courseId, Func<CodeForCoders.Commerce.Domain.Entities.CatalogCourseView, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
            var row = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogCourseViews.AsNoTracking()
                .SingleOrDefaultAsync(course => course.CourseId == courseId, Cancellation);
            if (row is not null && predicate(row)) return row;
            await Task.Delay(50, Cancellation);
        }
        throw new TimeoutException("The published course projection did not converge.");
    }

    private static async Task WaitUntilQueueEmptyAsync(CatalogCourseApiFactory factory)
    {
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await channel.MessageCountAsync("commerce.catalog-course", Cancellation) == 0)
            {
                // Broker count excludes the active delivery; allow its transaction to finish.
                await Task.Delay(200, Cancellation); return;
            }
            await Task.Delay(50, Cancellation);
        }
        throw new TimeoutException("The catalog queue did not drain.");
    }

    private static Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, Guid tenant,
        string permission = "oferta.editar", string role = "financeiro", string path = "")
    {
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", Guid.CreateVersion7().ToString()), new Claim("tenantId", tenant.ToString()), new Claim("roles", role), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/catalog/courses" + path);
        request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client.SendAsync(request, Cancellation);
    }
}
