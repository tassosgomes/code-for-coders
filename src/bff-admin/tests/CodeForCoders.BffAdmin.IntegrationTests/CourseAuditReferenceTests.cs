using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourseAuditReferenceTests
{
    [Fact(DisplayName = nameof(AdministratorWithoutAuthoringPermissionResolvesCourseOnAuditList))]
    public async Task AdministratorWithoutAuthoringPermissionResolvesCourseOnAuditList()
    {
        using var factory = Factory(); using var client = await factory.AuthenticatedAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("School course", page.GetProperty("data")[0].GetProperty("target").GetProperty("label").GetString());
        Assert.Equal("server-learning-token", factory.Learning.Token); Assert.Equal("server-audit-token", factory.Audit.Token);
        Assert.EndsWith("/course-references/resolve", factory.Learning.Uri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(AdministratorResolvesCourseOnAuditDetail))]
    public async Task AdministratorResolvesCourseOnAuditDetail()
    {
        using var factory = Factory(); using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("School course", detail.GetProperty("target").GetProperty("label").GetString());
        Assert.Equal("conteudo", detail.GetProperty("origin").GetString()); Assert.Equal(JsonValueKind.Null, detail.GetProperty("reason").ValueKind);
    }

    [Fact(DisplayName = nameof(LearningFailureKeepsOpaqueCourseReferenceOnListAndDetail))]
    public async Task LearningFailureKeepsOpaqueCourseReferenceOnListAndDetail()
    {
        using var factory = Factory(); factory.Learning.ReferencesUnavailable = true; using var client = await factory.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/audit-records/{factory.Audit.RecordId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(factory.Learning.CourseId, detail.GetProperty("target").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("target").GetProperty("label").ValueKind);
        using var list = await client.PostAsJsonAsync("/api/v1/audit-record-searches", new { _page = 1, _size = 20 }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, page.GetProperty("data")[0].GetProperty("target").GetProperty("label").ValueKind);
    }

    private static CourseBffApiFactory Factory()
    {
        var factory = new CourseBffApiFactory(); factory.Identity.Roles = ["administrador"]; factory.Identity.Permissions = []; return factory;
    }
}
