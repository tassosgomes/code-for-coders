using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class OfferAuditReferenceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdministratorWithoutEditPermissionResolvesOfferOnListWithCommerceAudienceToken()
    {
        using var factory = Factory();
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("Course — Option", page.GetProperty("data")[0].GetProperty("target").GetProperty("label").GetString());
        Assert.Equal("server-commerce-token", factory.OfferReferences.Token);
        Assert.Equal("server-audit-token", factory.Audit.Token);
        Assert.Equal("/internal/v1/offer-references/resolve", factory.OfferReferences.Uri!.AbsolutePath);
        Assert.Equal(new[] { factory.Audit.CourseId }, Assert.Single(factory.OfferReferences.Batches));
        Assert.Null(factory.Learning.Uri);
    }

    [Fact]
    public async Task AdministratorResolvesOfferOnDetailWithoutChangingAuditSchema()
    {
        using var factory = Factory();
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("oferta", detail.GetProperty("target").GetProperty("type").GetString());
        Assert.Equal("Course — Option", detail.GetProperty("target").GetProperty("label").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("reason").ValueKind);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("timeout")]
    [InlineData("malformed")]
    [InlineData("shape")]
    [InlineData("503")]
    [InlineData("403")]
    public async Task ResolverFailureKeepsOpaqueOfferOnListAndDetail(string failure)
    {
        using var factory = Factory();
        factory.OfferReferences.Failure = failure;
        using var client = await factory.AuthenticatedAsync();
        using var detail = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        AssertOpaque((await detail.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("target"), factory.Audit.CourseId);
        using var response = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOpaque((await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("data")[0].GetProperty("target"), factory.Audit.CourseId);
    }

    [Fact]
    public async Task UnknownOrDeletedOfferRemainsOpaqueWithoutError()
    {
        using var factory = Factory();
        factory.OfferReferences.Unknown = true;
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOpaque((await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("target"), factory.Audit.CourseId);
    }

    [Fact]
    public async Task DistinctOffersAreResolvedInBatchesOfFifty()
    {
        using var factory = Factory();
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.CreateVersion7()).ToArray();
        factory.Audit.ListTargets = [.. ids, ids[0]];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 50 }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 50, 1 }, factory.OfferReferences.Batches.Select(batch => batch.Length));
        Assert.Equal(ids.Order(), factory.OfferReferences.Batches.SelectMany(batch => batch).Order());
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.All(page.GetProperty("data").EnumerateArray(), row => Assert.Equal("Course — Option", row.GetProperty("target").GetProperty("label").GetString()));
    }

    [Fact]
    public async Task NonAdministratorCannotReadOfferAuditOrCallResolver()
    {
        using var factory = Factory();
        factory.Identity.Roles = ["financeiro"];
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.OfferReferences.Batches);
    }

    private static CourseBffApiFactory Factory()
    {
        var factory = new CourseBffApiFactory();
        factory.Identity.Roles = ["administrador"];
        factory.Identity.Permissions = [];
        factory.Audit.TargetType = "oferta";
        return factory;
    }

    private static void AssertOpaque(JsonElement reference, Guid id)
    {
        Assert.Equal(id, reference.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("label").ValueKind);
    }
}
