using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyAuditReferenceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(AdministratorResolvesStudentAuthorAndDerivedCourseTitleOnDetail))]
    public async Task AdministratorResolvesStudentAuthorAndDerivedCourseTitleOnDetail()
    {
        using var factory = new CourtesyAuditBffApiFactory();
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("cortesia-concedida", detail.GetProperty("type").GetString());
        Assert.Equal("Joana Ribeiro", detail.GetProperty("target").GetProperty("label").GetString());
        Assert.Equal("Marina Costa", detail.GetProperty("author").GetProperty("label").GetString());
        Assert.Equal("School course", detail.GetProperty("attributes").GetProperty("cursoTitulo").GetString());
        Assert.Equal(factory.Learning.CourseId.ToString("D"), detail.GetProperty("attributes").GetProperty("curso").GetString());
        Assert.Equal("6m", detail.GetProperty("attributes").GetProperty("vigencia").GetString());
        Assert.Equal("Bolsa integral do parceiro municipal", detail.GetProperty("reason").GetString());
        Assert.False(factory.Audit.LastAttributes!.ContainsKey("cursoTitulo"));
        Assert.Equal("server-learning-token", factory.Learning.Token);
        Assert.Equal("server-audit-token", factory.Audit.Token);
        Assert.Equal("/internal/v1/course-references/resolve", factory.Learning.Uri!.AbsolutePath);
        Assert.Equal(factory.Learning.CourseId, Assert.Single(factory.Learning.Payload!.Value.GetProperty("courseIds").EnumerateArray()).GetGuid());
        AssertIdentityBoundary(factory);
        Assert.DoesNotContain("email", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(ListResolvesStudentAndForwardsCourtesyTypeFilter))]
    public async Task ListResolvesStudentAndForwardsCourtesyTypeFilter()
    {
        using var factory = new CourtesyAuditBffApiFactory();
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20, type = "cortesia-concedida" }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("Joana Ribeiro", page.GetProperty("data")[0].GetProperty("target").GetProperty("label").GetString());
        Assert.Equal("cortesia-concedida", factory.Audit.Search!.Value.GetProperty("type").GetString());
        AssertIdentityBoundary(factory);
        Assert.Null(factory.Learning.Uri);
    }

    [Theory(DisplayName = nameof(UnresolvedStudentOrIdentityFailureKeepsOpaqueStudent))]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task UnresolvedStudentOrIdentityFailureKeepsOpaqueStudent(HttpStatusCode status)
    {
        using var factory = new CourtesyAuditBffApiFactory();
        factory.Identity.Unresolved = true;
        factory.Identity.ReferenceStatus = status;
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(factory.Audit.StudentId, detail.GetProperty("target").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("target").GetProperty("label").ValueKind);
        Assert.Equal("School course", detail.GetProperty("attributes").GetProperty("cursoTitulo").GetString());
        using var list = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(JsonValueKind.Null, page.GetProperty("data")[0].GetProperty("target").GetProperty("label").ValueKind);
    }

    [Theory(DisplayName = nameof(LearningFailureOrUnknownCourseKeepsOnlyCourseIdentifier))]
    [InlineData("unavailable")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    public async Task LearningFailureOrUnknownCourseKeepsOnlyCourseIdentifier(string failure)
    {
        using var factory = new CourtesyAuditBffApiFactory();
        factory.Learning.ReferencesUnavailable = failure == "unavailable";
        factory.Learning.Malformed = failure == "malformed";
        if (failure == "unknown") factory.Audit.CourseReference = Guid.CreateVersion7().ToString("D");
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.False(detail.GetProperty("attributes").TryGetProperty("cursoTitulo", out _));
        Assert.Equal(factory.Audit.CourseReference ?? factory.Learning.CourseId.ToString("D"), detail.GetProperty("attributes").GetProperty("curso").GetString());
        Assert.Equal("Joana Ribeiro", detail.GetProperty("target").GetProperty("label").GetString());
    }

    [Theory(DisplayName = nameof(NonAdministratorCannotReadCourtesyListOrDetail))]
    [InlineData("financeiro")]
    [InlineData("professor")]
    public async Task NonAdministratorCannotReadCourtesyListOrDetail(string role)
    {
        using var factory = new CourtesyAuditBffApiFactory();
        factory.Identity.Roles = [role];
        using var client = await factory.AuthenticatedAsync();
        using var detail = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        using var list = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(0, factory.Audit.Calls);
        Assert.Empty(factory.Identity.References);
        Assert.Null(factory.Learning.Uri);
    }

    [Fact(DisplayName = nameof(AdministratorRoleRevokedDuringReferenceLookupReturnsForbidden))]
    public async Task AdministratorRoleRevokedDuringReferenceLookupReturnsForbidden()
    {
        using var factory = new CourtesyAuditBffApiFactory();
        factory.Identity.ReferenceStatus = HttpStatusCode.Forbidden;
        using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("PERMISSION_DENIED", problem.GetProperty("code").GetString());
    }

    private static void AssertIdentityBoundary(CourtesyAuditBffApiFactory factory)
    {
        Assert.Contains(factory.Identity.References, item => item.Type == "conta-aluno" && item.Id == factory.Audit.StudentId);
        Assert.Equal(factory.Identity.ExpectedSessionId, factory.Identity.ReceivedSessionId);
        Assert.Equal("audit-references:read", factory.Identity.AssertionScope);
    }
}
