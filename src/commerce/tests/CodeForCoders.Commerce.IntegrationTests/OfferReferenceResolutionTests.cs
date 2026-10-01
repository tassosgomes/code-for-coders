using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OfferReferenceResolutionTests(CommerceIntegrationFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();
    private const string Path = "/internal/v1/offer-references/resolve";

    [Fact]
    public async Task AdministratorWithoutEditPermissionResolvesFiftyCurrentLabelsWithoutSideEffects()
    {
        await using var factory = new CatalogCourseApiFactory(fixture);
        var ids = await SeedAsync(factory, 50);
        using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, new { offerIds = ids });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var references = await response.Content.ReadFromJsonAsync<OfferReferenceList>(Cancellation);
        Assert.Equal(50, references!.Data.Count);
        Assert.All(references.Data, reference => Assert.Equal("Course title — Option", reference.Label));
        Assert.Equal(ids.Order(), references.Data.Select(reference => reference.OfferId).Order());
        await using var scope = Scope(factory);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
    }

    [Fact]
    public async Task MissingAdministratorRoleReturnsPermissionDeniedEvenWithEditPermission()
    {
        await using var factory = new CatalogCourseApiFactory(fixture);
        using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, new { offerIds = new[] { Guid.CreateVersion7() } }, role: "financeiro");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("PERMISSION_DENIED", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task OtherTenantUnknownAndDeletedOffersAreIndistinguishablyOmitted()
    {
        await using var factory = new CatalogCourseApiFactory(fixture);
        var ids = await SeedAsync(factory, 2);
        await using (var scope = Scope(factory))
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            db.CatalogOffers.Remove(await db.CatalogOffers.SingleAsync(offer => offer.OfferId == ids[1], Cancellation));
            await db.SaveChangesAsync(Cancellation);
        }
        using var client = factory.CreateClient();
        using var response = await SendAsync(factory, client, new { offerIds = new[] { ids[0], ids[1], Guid.CreateVersion7() } });
        var own = await response.Content.ReadFromJsonAsync<OfferReferenceList>(Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ids[0], Assert.Single(own!.Data).OfferId);
        using var foreign = await SendAsync(factory, client, new { offerIds = ids }, tenant: Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Empty((await foreign.Content.ReadFromJsonAsync<OfferReferenceList>(Cancellation))!.Data);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("over-limit")]
    [InlineData("duplicate")]
    [InlineData("invalid-uuid")]
    [InlineData("missing")]
    [InlineData("extra")]
    public async Task InvalidBatchReturns400(string scenario)
    {
        await using var factory = new CatalogCourseApiFactory(fixture);
        using var client = factory.CreateClient();
        var id = Guid.CreateVersion7();
        object body = scenario switch
        {
            "empty" => new { offerIds = Array.Empty<Guid>() },
            "over-limit" => new { offerIds = Enumerable.Range(0, 51).Select(_ => Guid.CreateVersion7()).ToArray() },
            "duplicate" => new { offerIds = new[] { id, id } },
            "invalid-uuid" => new { offerIds = new[] { "invalid" } },
            "extra" => new { offerIds = new[] { id }, tenantId = _tenant },
            _ => new { }
        };
        using var response = await SendAsync(factory, client, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_REQUEST", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task MissingOrInvalidTokenReturns401()
    {
        await using var factory = new CatalogCourseApiFactory(fixture);
        using var client = factory.CreateClient();
        using var missing = await client.PostAsJsonAsync(Path, new { offerIds = new[] { Guid.CreateVersion7() } }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        using var invalid = await client.PostAsJsonAsync(Path, new { offerIds = new[] { Guid.CreateVersion7() } }, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    private async Task<Guid[]> SeedAsync(CatalogCourseApiFactory factory, int count)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>()
                .ApplyAsync(PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(_tenant, _course, title: "Course title")), Cancellation);
        await using var write = Scope(factory);
        var db = write.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.SingleAsync(Cancellation);
        var ids = Enumerable.Range(0, count).Select(_ => course.CreateOffer(new("Option", 49700,
            AccessPeriod.Create("months", 12)), DateTimeOffset.UtcNow).OfferId).ToArray();
        await db.SaveChangesAsync(Cancellation);
        return ids;
    }

    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant);
        return scope;
    }

    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, object body,
        string role = "administrador", Guid? tenant = null)
    {
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", Guid.CreateVersion7().ToString()), new Claim("tenantId", (tenant ?? _tenant).ToString()),
             new Claim("roles", role), new Claim("permissions", role == "administrador" ? "" : "oferta.editar")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2),
            new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client.SendAsync(request, Cancellation);
    }
}
