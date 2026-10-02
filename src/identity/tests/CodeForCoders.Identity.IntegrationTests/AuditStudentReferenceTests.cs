using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(StudentAccountLookupCollection.Name)]
public sealed class AuditStudentReferenceTests(StudentAccountLookupFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(AdministratorResolvesStudentNameWithoutEmail))]
    public async Task AdministratorResolvesStudentNameWithoutEmail()
    {
        var email = $"{Guid.CreateVersion7()}@audit.test";
        var id = await fixture.RegisterAsync(email);
        using var response = await ResolveAsync(await fixture.LoginAsync(StaffRoleCatalog.Administrator), [id]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(Cancellation);
        using var document = JsonDocument.Parse(content);
        var reference = Assert.Single(document.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal("conta-aluno", reference.GetProperty("type").GetString());
        Assert.Equal(id, reference.GetProperty("id").GetGuid());
        Assert.Equal("Lookup Student", reference.GetProperty("label").GetString());
        Assert.DoesNotContain(email, content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, reference.EnumerateObject().Count());
    }

    [Fact(DisplayName = nameof(DeactivatedStudentStillResolvesByName))]
    public async Task DeactivatedStudentStillResolvesByName()
    {
        var id = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@audit.test");
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var account = await db.Accounts.IgnoreQueryFilters().SingleAsync(item => item.Id == id, Cancellation);
            db.Entry(account).Property(item => item.DeactivatedOn).CurrentValue = TimeProvider.System.GetUtcNow();
            await db.SaveChangesAsync(Cancellation);
        }

        using var response = await ResolveAsync(await fixture.LoginAsync(StaffRoleCatalog.Administrator), [id]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("Lookup Student", document.GetProperty("data")[0].GetProperty("label").GetString());
    }

    [Fact(DisplayName = nameof(ForeignMissingAndInternalAccountsRemainIndistinguishable))]
    public async Task ForeignMissingAndInternalAccountsRemainIndistinguishable()
    {
        var foreignId = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@audit.test", fixture.OtherTenantId);
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Administrator);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var internalId = await db.StaffSessions.IgnoreQueryFilters().Where(item => item.Id == sessionId)
            .Select(item => item.AccountId).SingleAsync(Cancellation);
        var ids = new[] { foreignId, Guid.CreateVersion7(), internalId };
        using var response = await ResolveAsync(sessionId, ids);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var references = document.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(ids, references.Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(references, item =>
        {
            Assert.Equal(3, item.EnumerateObject().Count());
            Assert.Equal("conta-aluno", item.GetProperty("type").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("label").ValueKind);
        });
    }

    [Fact(DisplayName = nameof(NonAdministratorCannotResolveStudentReferences))]
    public async Task NonAdministratorCannotResolveStudentReferences()
    {
        foreach (var role in new[] { StaffRoleCatalog.Finance, StaffRoleCatalog.Teacher, StaffRoleCatalog.Support })
        {
            using var response = await ResolveAsync(await fixture.LoginAsync(role), [Guid.CreateVersion7()]);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var document = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.Equal("PERMISSION_DENIED", document.GetProperty("code").GetString());
        }
    }

    private async Task<HttpResponseMessage> ResolveAsync(Guid sessionId, IReadOnlyList<Guid> ids)
    {
        using var client = fixture.App.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/audit-identity-reference-lookups")
        {
            Content = JsonContent.Create(new { references = ids.Select(id => new { type = "conta-aluno", id }) }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Assertion("audit-references:read"));
        request.Headers.Add("X-Staff-Session", sessionId.ToString("D"));
        return await client.SendAsync(request, Cancellation);
    }
}
