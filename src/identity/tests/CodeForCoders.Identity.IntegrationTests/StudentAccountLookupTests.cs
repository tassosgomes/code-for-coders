using System.Net;
using System.Text.Json;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Domain.Entities;
using CodeForCoders.Identity.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

[Collection(StudentAccountLookupCollection.Name)]
public sealed class StudentAccountLookupTests(StudentAccountLookupFixture fixture)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static string Email() => $"{Guid.CreateVersion7()}@student.test";

    [Fact(DisplayName = nameof(NormalizesEmailAndReturnsOnlyMinimalStudentDataWithoutPersonalTelemetry))]
    public async Task NormalizesEmailAndReturnsOnlyMinimalStudentDataWithoutPersonalTelemetry()
    {
        var email = Email();
        var studentId = await fixture.RegisterAsync(email);
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        while (fixture.Logs.TryDequeue(out _)) { }
        while (fixture.Spans.TryDequeue(out _)) { }
        using var response = await fixture.LookupAsync(sessionId, new { email = "  " + email.ToUpperInvariant() + "  " }, fixture.Assertion());
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(studentId, document.RootElement.GetProperty("studentId").GetGuid());
        Assert.Equal(email, document.RootElement.GetProperty("email").GetString());
        Assert.Equal("Lookup Student", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("active", document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(5, document.RootElement.EnumerateObject().Count());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var token = await fixture.SessionTokenAsync(sessionId);
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')));
        Assert.Contains("cortesia.conceder", claims.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
        Assert.NotEmpty(fixture.Logs);
        Assert.NotEmpty(fixture.Spans);
        Assert.All(fixture.Logs.Concat(fixture.Spans), entry =>
        {
            Assert.DoesNotContain(email, entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Lookup Student", entry, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = nameof(MissingInternalAndForeignAccountsHaveIdenticalNotFoundResponses))]
    public async Task MissingInternalAndForeignAccountsHaveIdenticalNotFoundResponses()
    {
        var foreignEmail = Email();
        await fixture.RegisterAsync(foreignEmail, fixture.OtherTenantId);
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var internalEmail = await db.Accounts.IgnoreQueryFilters().Join(db.StaffSessions.IgnoreQueryFilters(), account => account.Id, session => session.AccountId,
            (account, session) => new { account.Email, session.Id }).Where(pair => pair.Id == sessionId).Select(pair => pair.Email).SingleAsync(CancellationToken);
        foreach (var email in new[] { Email(), internalEmail, foreignEmail })
        {
            using var response = await fixture.LookupAsync(sessionId, new { email }, fixture.Assertion());
            await AssertProblemAsync(response, HttpStatusCode.NotFound, "STUDENT_ACCOUNT_NOT_FOUND");
            using var document = await ReadAsync(response);
            Assert.Equal("Student account not found.", document.RootElement.GetProperty("title").GetString());
            Assert.DoesNotContain(email, document.RootElement.GetRawText());
        }
    }

    [Fact(DisplayName = nameof(DisabledStudentIsReturnedAndConfirmedStatusIsPreserved))]
    public async Task DisabledStudentIsReturnedAndConfirmedStatusIsPreserved()
    {
        var email = Email(); var id = await fixture.RegisterAsync(email);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var account = await db.Accounts.IgnoreQueryFilters().SingleAsync(account => account.Id == id, CancellationToken);
            account.Confirm();
            db.Entry(account).Property(account => account.DeactivatedOn).CurrentValue = TimeProvider.System.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken);
        }
        using var response = await fixture.LookupAsync(await fixture.LoginAsync(StaffRoleCatalog.Finance), new { email }, fixture.Assertion());
        using var document = await ReadAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("disabled", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.GetProperty("emailConfirmed").GetBoolean());
        var currentId = await fixture.RegisterAsync(email);
        using var current = await fixture.LookupAsync(await fixture.LoginAsync(StaffRoleCatalog.Finance), new { email }, fixture.Assertion());
        using var currentDocument = await ReadAsync(current);
        Assert.Equal(currentId, currentDocument.RootElement.GetProperty("studentId").GetGuid());
        Assert.Equal("active", currentDocument.RootElement.GetProperty("status").GetString());

    }

    [Fact(DisplayName = nameof(RequiresValidServiceAssertionIncludingSignatureAndTenant))]
    public async Task RequiresValidServiceAssertionIncludingSignatureAndTenant()
    {
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        using var otherKey = System.Security.Cryptography.RSA.Create(2048);
        foreach (var assertion in new[] { null, "invalid", fixture.Assertion(key: otherKey), fixture.Assertion(tenantId: Guid.CreateVersion7()) })
        {
            using var response = await fixture.LookupAsync(sessionId, new { email = Email() }, assertion);
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED");
        }
    }

    [Fact(DisplayName = nameof(RequiresLookupScopeAndCurrentStaffSession))]
    public async Task RequiresLookupScopeAndCurrentStaffSession()
    {
        var session = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        using var noScope = await fixture.LookupAsync(session, new { email = Email() }, fixture.Assertion("staff-members:read"));
        await AssertProblemAsync(noScope, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        foreach (var id in new Guid?[] { null, Guid.CreateVersion7() })
        {
            using var response = await fixture.LookupAsync(id, new { email = Email() }, fixture.Assertion());
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        }
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var entity = await db.StaffSessions.IgnoreQueryFilters().SingleAsync(item => item.Id == session, CancellationToken);
            entity.Revoke(TimeProvider.System.GetUtcNow()); await db.SaveChangesAsync(CancellationToken);
        }
        using var revoked = await fixture.LookupAsync(session, new { email = Email() }, fixture.Assertion());
        await AssertProblemAsync(revoked, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
    }

    [Fact(DisplayName = nameof(TeacherSupportAdministratorAndStudentCannotLookup))]
    public async Task TeacherSupportAdministratorAndStudentCannotLookup()
    {
        foreach (var role in new[] { StaffRoleCatalog.Teacher, StaffRoleCatalog.Support, StaffRoleCatalog.Administrator, null })
        {
            using var response = await fixture.LookupAsync(await fixture.LoginAsync(role), new { email = Email() }, fixture.Assertion());
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }
        using var student = await fixture.LookupAsync(await fixture.LoginAsync(null, student: true), new { email = Email() }, fixture.Assertion());
        await AssertProblemAsync(student, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
    }

    [Fact(DisplayName = nameof(RevokingFinanceRoleDeniesTheNextActionOnTheOpenSession))]
    public async Task RevokingFinanceRoleDeniesTheNextActionOnTheOpenSession()
    {
        var email = Email(); await fixture.RegisterAsync(email);
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        using var first = await fixture.LookupAsync(sessionId, new { email }, fixture.Assertion());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var accountId = await db.StaffSessions.IgnoreQueryFilters().Where(session => session.Id == sessionId).Select(session => session.AccountId).SingleAsync(CancellationToken);
            await db.StaffRoleAssignments.IgnoreQueryFilters().Where(role => role.AccountId == accountId).ExecuteDeleteAsync(CancellationToken);
        }
        using var next = await fixture.LookupAsync(sessionId, new { email }, fixture.Assertion());
        await AssertProblemAsync(next, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
    }

    [Fact(DisplayName = nameof(RejectsInvalidEmailAndUnknownPropertiesWithoutEchoingInput))]
    public async Task RejectsInvalidEmailAndUnknownPropertiesWithoutEchoingInput()
    {
        var sessionId = await fixture.LoginAsync(StaffRoleCatalog.Finance);
        foreach (var body in new object[] { new { email = "invalid" }, new { email = "" }, new { }, new { email = Email(), tenantId = fixture.OtherTenantId }, new { email = new string('a', 255) + "@x.test" } })
        {
            using var response = await fixture.LookupAsync(sessionId, body, fixture.Assertion());
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
        }
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = await ReadAsync(response);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }
}
