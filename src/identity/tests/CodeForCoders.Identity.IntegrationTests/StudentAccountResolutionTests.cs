using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CodeForCoders.Identity.IntegrationTests;

[Collection(StudentAccountLookupCollection.Name)]
public sealed class StudentAccountResolutionTests(StudentAccountLookupFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact(DisplayName = nameof(BatchOmitsUnknownInternalAndOtherTenantAndKeepsDisabledAccounts))]
    public async Task BatchOmitsUnknownInternalAndOtherTenantAndKeepsDisabledAccounts()
    {
        var email = $"{Guid.CreateVersion7()}@resolution.test"; var active = await fixture.RegisterAsync(email);
        var disabled = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@resolution.test"); var other = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@resolution.test", fixture.OtherTenantId);
        var session = await fixture.LoginAsync("financeiro"); Guid staff;
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(fixture.TenantId); var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var account = await db.Accounts.IgnoreQueryFilters().SingleAsync(account => account.Id == disabled, Cancellation); db.Entry(account).Property(item => item.DeactivatedOn).CurrentValue = DateTimeOffset.UtcNow;
            staff = await db.StaffSessions.IgnoreQueryFilters().Where(item => item.Id == session).Select(item => item.AccountId).SingleAsync(Cancellation); await db.SaveChangesAsync(Cancellation);
        }
        using var response = await ResolveAsync(session, new { studentIds = new[] { active, disabled, other, staff, Guid.CreateVersion7() } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await BodyAsync(response); var data = body.GetProperty("data").EnumerateArray().ToArray(); Assert.Equal(2, data.Length);
        Assert.Equal(email, data.Single(row => row.GetProperty("studentId").GetGuid() == active).GetProperty("email").GetString());
        Assert.Equal("disabled", data.Single(row => row.GetProperty("studentId").GetGuid() == disabled).GetProperty("status").GetString());
        Assert.All(fixture.Logs.Concat(fixture.Spans), entry => Assert.DoesNotContain(email, entry));
    }
    [Fact(DisplayName = nameof(InvalidBatchIsRejectedWithoutEchoingInput))]
    public async Task InvalidBatchIsRejectedWithoutEchoingInput()
    {
        var session = await fixture.LoginAsync("financeiro"); var id = Guid.CreateVersion7();
        foreach (var body in new object[] { new { studentIds = Enumerable.Range(0, 51).Select(_ => Guid.CreateVersion7()).ToArray() }, new { studentIds = new[] { id, id } }, new { studentIds = Array.Empty<Guid>() }, new { studentIds = new[] { Guid.Empty } }, new { studentIds = new[] { id }, tenantId = fixture.OtherTenantId }, new { studentIds = new[] { "invalid" } } })
        { using var response = await ResolveAsync(session, body); await ProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR"); }
    }
    [Fact(DisplayName = nameof(NonFinanceAndRevokedRoleCannotResolveOnAnOpenSession))]
    public async Task NonFinanceAndRevokedRoleCannotResolveOnAnOpenSession()
    {
        foreach (var role in new[] { "professor", "suporte", "administrador" })
        { using var response = await ResolveAsync(await fixture.LoginAsync(role), new { studentIds = new[] { Guid.CreateVersion7() } }); await ProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED"); }
        var session = await fixture.LoginAsync("financeiro");
        using var first = await ResolveAsync(session, new { studentIds = new[] { Guid.CreateVersion7() } }); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>(); var account = await db.StaffSessions.IgnoreQueryFilters().Where(item => item.Id == session).Select(item => item.AccountId).SingleAsync(Cancellation);
            await db.StaffRoleAssignments.IgnoreQueryFilters().Where(role => role.AccountId == account).ExecuteDeleteAsync(Cancellation);
        }
        using var revoked = await ResolveAsync(session, new { studentIds = new[] { Guid.CreateVersion7() } }); await ProblemAsync(revoked, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
    }
    [Fact(DisplayName = nameof(RequiresResolveScopeAndCurrentSameTenantSession))]
    public async Task RequiresResolveScopeAndCurrentSameTenantSession()
    {
        var session = await fixture.LoginAsync("financeiro"); var body = new { studentIds = new[] { Guid.CreateVersion7() } };
        using var wrongScope = await ResolveAsync(session, body, fixture.Assertion("student-account:lookup")); await ProblemAsync(wrongScope, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        using var missing = await ResolveAsync(null, body); await ProblemAsync(missing, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        using var other = await ResolveAsync(session, body, fixture.Assertion("student-account:resolve", fixture.OtherTenantId)); await ProblemAsync(other, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        using var student = await ResolveAsync(await fixture.LoginAsync(null, student: true), body); await ProblemAsync(student, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
    }
    private async Task<HttpResponseMessage> ResolveAsync(Guid? session, object body, string? assertion = null)
    {
        using var client = fixture.App.GetTestClient(); using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/student-account-resolutions") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion ?? fixture.Assertion("student-account:resolve"));
        if (session is not null) request.Headers.Add("X-Staff-Session", session.ToString()); return await client.SendAsync(request, Cancellation);
    }
    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    { using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)); return doc.RootElement.Clone(); }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await BodyAsync(response)).GetProperty("code").GetString()); }
}
