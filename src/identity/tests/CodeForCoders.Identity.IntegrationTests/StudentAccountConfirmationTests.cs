using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CodeForCoders.Identity.IntegrationTests;

[Collection(StudentAccountLookupCollection.Name)]
public sealed class StudentAccountConfirmationTests(StudentAccountLookupFixture fixture)
{
    [Fact(DisplayName = nameof(ActiveStudentIsEligibleWithoutEmailConfirmation))]
    public async Task ActiveStudentIsEligibleWithoutEmailConfirmation()
    {
        var student = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@confirm.test");
        using var response = await SendAsync(student); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"eligible\":true}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
    [Theory(DisplayName = nameof(IneligibleAccountsAreIndistinguishable))]
    [InlineData("disabled")]
    [InlineData("internal")]
    [InlineData("other-tenant")]
    [InlineData("unknown")]
    public async Task IneligibleAccountsAreIndistinguishable(string kind)
    {
        var id = Guid.CreateVersion7(); await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (kind != "unknown")
        {
            var tenant = kind == "other-tenant" ? fixture.OtherTenantId : fixture.TenantId;
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
            var email = $"{id}@confirm.test";
            db.Accounts.Add(kind == "internal" ? Account.CreateInternal(id, tenant, "Internal actor", email, email) : Account.CreateStudent(id, tenant, "Student", email, email));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (kind == "disabled") await db.Accounts.IgnoreQueryFilters().Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.DeactivatedOn, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        }
        using var response = await SendAsync(id); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"eligible\":false}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact(DisplayName = nameof(AssertionAuthenticationAndScopeAreEnforced))]
    public async Task AssertionAuthenticationAndScopeAreEnforced()
    {
        using var absent = await SendAsync(Guid.CreateVersion7(), ""); Assert.Equal(HttpStatusCode.Unauthorized, absent.StatusCode);
        using var wrongScope = await SendAsync(Guid.CreateVersion7(), fixture.Assertion("staff-sessions:validate", issuer: "commerce")); Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        using var otherTenant = await SendAsync(Guid.CreateVersion7(), fixture.Assertion("student-account:confirm", fixture.OtherTenantId, issuer: "commerce")); Assert.Equal(HttpStatusCode.Unauthorized, otherTenant.StatusCode);
    }
    private async Task<HttpResponseMessage> SendAsync(Guid studentId, string? assertion = null)
    {
        using var client = fixture.App.GetTestClient(); using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/student-account-confirmations") { Content = JsonContent.Create(new { studentId }) };
        var token = assertion ?? fixture.Assertion("student-account:confirm", issuer: "commerce");
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
