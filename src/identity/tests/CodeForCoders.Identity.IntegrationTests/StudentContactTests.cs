using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(StudentAccountLookupCollection.Name)]
public sealed class StudentContactTests(StudentAccountLookupFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(ActiveContactIsMinimalAndAbsentFromTelemetry))]
    public async Task ActiveContactIsMinimalAndAbsentFromTelemetry()
    {
        var email = $"{Guid.CreateVersion7()}@contact.test";
        var id = await fixture.RegisterAsync(email);
        using var response = await GetAsync(id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        StudentAccountLookupContract.AssertContact(json.RootElement);
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
        Assert.Equal(id, json.RootElement.GetProperty("studentId").GetGuid());
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
        Assert.Equal("Lookup Student", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("active", json.RootElement.GetProperty("status").GetString());
        Assert.All(fixture.Logs.Concat(fixture.Spans), entry => Assert.DoesNotContain(email, entry, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = nameof(DisabledContactIsReturnedAsDisabled))]
    public async Task DisabledContactIsReturnedAsDisabled()
    {
        var id = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@contact.test");
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var account = await db.Accounts.IgnoreQueryFilters().SingleAsync(a => a.Id == id, Cancellation);
            db.Entry(account).Property(a => a.DeactivatedOn).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Cancellation);
        }
        using var response = await GetAsync(id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("disabled", json.RootElement.GetProperty("status").GetString());
    }

    [Fact(DisplayName = nameof(InternalAndMissingAccountsAreIndistinguishable))]
    public async Task InternalAndMissingAccountsAreIndistinguishable()
    {
        var id = Guid.CreateVersion7();
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Accounts.Add(Account.CreateInternal(id, fixture.TenantId, "Internal", "internal@contact.test", "internal@contact.test"));
            await db.SaveChangesAsync(Cancellation);
        }
        foreach (var account in new[] { id, Guid.CreateVersion7() })
        {
            using var response = await GetAsync(account);
            await NotFoundAsync(response);
        }
    }

    [Fact(DisplayName = nameof(ForeignTenantAccountIsNotFound))]
    public async Task ForeignTenantAccountIsNotFound()
    {
        var id = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@contact.test", fixture.OtherTenantId);
        using var response = await GetAsync(id);
        await NotFoundAsync(response);
    }

    [Fact(DisplayName = nameof(MissingScopeOrWrongIssuerIsForbidden))]
    public async Task MissingScopeOrWrongIssuerIsForbidden()
    {
        foreach (var token in new[] { fixture.Assertion("other-scope", issuer: "notification"), fixture.Assertion("student-account:confirm", issuer: "commerce") })
        {
            using var response = await GetAsync(Guid.CreateVersion7(), token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Fact(DisplayName = nameof(AssertionReplayIsUnauthorized))]
    public async Task AssertionReplayIsUnauthorized()
    {
        var id = await fixture.RegisterAsync($"{Guid.CreateVersion7()}@contact.test");
        var token = fixture.Assertion("student-contact:read", issuer: "notification");
        using var first = await GetAsync(id, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replay = await GetAsync(id, token);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid id, string? token = null)
    {
        using var client = fixture.App.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/student-accounts/{id:D}/contact");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? fixture.Assertion("student-contact:read", issuer: "notification"));
        return await client.SendAsync(request, Cancellation);
    }

    private static async Task NotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("STUDENT_ACCOUNT_NOT_FOUND", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Conta de aluno não encontrada.", json.RootElement.GetProperty("title").GetString());
        Assert.False(json.RootElement.TryGetProperty("instance", out _));
    }
}
