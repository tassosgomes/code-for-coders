using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.Common;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class CourtesyGrantTests(CommerceIntegrationFixture infra)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact(DisplayName = nameof(GrantCreatesEnrollmentReceiptFactAndActAtomicallyWithoutPersonalDataInFact))]
    public async Task GrantCreatesEnrollmentReceiptFactAndActAtomicallyWithoutPersonalDataInFact()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        using var response = await test.GrantAsync(); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var grant = (await response.Content.ReadFromJsonAsync<CourtesyGrant>(Cancellation))!;
        Assert.Equal(DateOnly.Parse("2027-04-15"), grant.EndsOn); Assert.Equal("courtesy", grant.Origin); Assert.Equal("active", grant.Status);
        Assert.EndsWith(grant.GrantId.ToString(), response.Headers.Location!.ToString());
        await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Single(await db.Enrollments.ToListAsync(Cancellation));
        Assert.Equal(test.Actor, (await db.AccessGrants.SingleAsync(Cancellation)).GrantedBy);
        var receipt = await db.GrantReceipts.SingleAsync(Cancellation); Assert.Equal(64, receipt.KeyHash.Length); Assert.NotEqual("courtesy-test-key", receipt.KeyHash);
        var messages = await db.EntitlementOutboxMessages.ToListAsync(Cancellation); Assert.Equal(2, messages.Count);
        var factMessage = messages.Single(item => item.RoutingKey == "matricula.acesso-concedido.v1");
        using var fact = JsonDocument.Parse(factMessage.Payload); Assert.Equal(12, fact.RootElement.EnumerateObject().Count());
        CourtesyGrantContract.AssertValid(fact.RootElement);
        Assert.False(fact.RootElement.TryGetProperty("reason", out _)); Assert.DoesNotContain("Bolsa", factMessage.Payload); Assert.DoesNotContain("email", factMessage.Payload); Assert.DoesNotContain("name", factMessage.Payload);
        using var act = JsonDocument.Parse(messages.Single(item => item.RoutingKey == "auditoria.ato-praticado.v1").Payload);
        CourtesyGrantContract.AssertValid(act.RootElement, true);
        Assert.Equal(fact.RootElement.GetProperty("eventId").GetGuid(), act.RootElement.GetProperty("fatoId").GetGuid());
        Assert.DoesNotContain(test.Logs.Concat(test.Spans), entry => entry.Contains("Bolsa de mentoria", StringComparison.Ordinal) || entry.Contains("courtesy-test-key", StringComparison.Ordinal));
        Assert.Equal("Bolsa de mentoria", act.RootElement.GetProperty("motivo").GetString()); Assert.Equal("6m", act.RootElement.GetProperty("complemento").GetProperty("vigencia").GetString());
        var assertion = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(test.Identity.Assertion);
        var settings = test.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CodeForCoders.Commerce.Api.Clients.StudentAccountIdentityOptions>>().Value;
        using var rsa = System.Security.Cryptography.RSA.Create(); rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(settings.SigningKeyBase64), out _);
        new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ValidateToken(test.Identity.Assertion, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        { ValidateLifetime = false, ValidIssuer = "commerce", ValidAudience = "identity-internal", IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) }, out _);
        Assert.Equal(test.Tenant.ToString(), assertion.Claims.Single(item => item.Type == "tenantId").Value);
        Assert.Equal("commerce", assertion.Issuer); Assert.Equal("student-account:confirm", assertion.Claims.Single(item => item.Type == "scope").Value);
        Assert.True(assertion.ValidTo - assertion.ValidFrom <= TimeSpan.FromSeconds(60));
        using var read = await test.Client.GetAsync(response.Headers.Location, Cancellation); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }
    [Fact(DisplayName = nameof(ConcurrentSameKeyCreatesOneGrantAndReplaysOriginalWithoutReconfirming))]
    public async Task ConcurrentSameKeyCreatesOneGrantAndReplaysOriginalWithoutReconfirming()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        var responses = await Task.WhenAll(test.GrantAsync(), test.GrantAsync());
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Created }, responses.Select(item => item.StatusCode).Order().ToArray());
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(Cancellation), await responses[1].Content.ReadAsStringAsync(Cancellation));
        foreach (var response in responses) response.Dispose();
        var calls = test.Identity.Calls; test.Identity.Unavailable = true;
        using var replay = await test.GrantAsync(); Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(calls, test.Identity.Calls);
        await using var scope = test.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal(1, await db.AccessGrants.IgnoreQueryFilters().CountAsync(item => item.TenantId == test.Tenant, Cancellation));
        Assert.Equal(2, await db.EntitlementOutboxMessages.IgnoreQueryFilters().CountAsync(item => item.TenantId == test.Tenant, Cancellation));
    }
    [Fact(DisplayName = nameof(ChangedBodyWithSameKeyIsRejected))]
    public async Task ChangedBodyWithSameKeyIsRejected()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); using var first = await test.GrantAsync();
        using var second = await test.GrantAsync(test.Body("Other reason")); await AssertCodeAsync(second, 422, "IDEMPOTENCY_KEY_REUSED");
    }
    [Fact(DisplayName = nameof(DifferentKeysAllowMultipleActiveGrantsOnOneEnrollment))]
    public async Task DifferentKeysAllowMultipleActiveGrantsOnOneEnrollment()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        var responses = await Task.WhenAll(test.GrantAsync(key: "first"), test.GrantAsync(key: "second"));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); response.Dispose(); }
        await using var scope = test.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal(1, await db.Enrollments.IgnoreQueryFilters().CountAsync(item => item.TenantId == test.Tenant, Cancellation));
        Assert.Equal(2, await db.AccessGrants.IgnoreQueryFilters().CountAsync(item => item.TenantId == test.Tenant, Cancellation));
    }
    [Theory(DisplayName = nameof(InvalidReasonAndMonthsRejectWithoutAnyWrite))]
    [InlineData("", 6)]
    [InlineData("   ", 6)]
    [InlineData("long", 6)]
    [InlineData("Valid reason", 0)]
    [InlineData("Valid reason", 61)]
    public async Task InvalidReasonAndMonthsRejectWithoutAnyWrite(string reason, int months)
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        using var response = await test.GrantAsync(test.Body(reason == "long" ? new string('a', 501) : reason, months));
        await AssertCodeAsync(response, 422, "FIELD_INVALID"); Assert.Equal(0, test.Identity.Calls); await test.AssertEmptyAsync();
    }
    [Theory(DisplayName = nameof(UnpublishedAndOtherTenantCoursesRejectWithoutIdentityCheck))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpublishedAndOtherTenantCoursesRejectWithoutIdentityCheck(bool otherTenant)
    {
        await using var test = new CourtesyGrantFixture(infra); if (otherTenant) { await test.SeedAsync(); test.Authorize(Guid.CreateVersion7()); }
        using var response = await test.GrantAsync(); await AssertCodeAsync(response, 422, "COURSE_NOT_ELIGIBLE"); Assert.Equal(0, test.Identity.Calls); await test.AssertEmptyAsync();
    }
    [Fact(DisplayName = nameof(ReconfirmedIneligibleAccountRejectsWithoutWrite))]
    public async Task ReconfirmedIneligibleAccountRejectsWithoutWrite()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); test.Identity.Response = "{\"eligible\":false}";
        using var response = await test.GrantAsync(); await AssertCodeAsync(response, 422, "STUDENT_ACCOUNT_NOT_ELIGIBLE"); await test.AssertEmptyAsync();
    }
    [Theory(DisplayName = nameof(UnavailableMalformedAndTimedOutIdentityFailClosedWithoutRetry))]
    [InlineData("unavailable")]
    [InlineData("malformed")]
    [InlineData("timeout")]
    public async Task UnavailableMalformedAndTimedOutIdentityFailClosedWithoutRetry(string mode)
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); test.Identity.Unavailable = mode == "unavailable";
        test.Identity.Timeout = mode == "timeout"; if (mode == "malformed") test.Identity.Response = "{\"eligible\":\"true\"}";
        using var response = await test.GrantAsync(); await AssertCodeAsync(response, 503, "STUDENT_ACCOUNT_CHECK_UNAVAILABLE");
        Assert.Equal(1, test.Identity.Calls); await test.AssertEmptyAsync();
    }
    [Fact(DisplayName = nameof(LifetimeHasNullDatesAndRejectsMonths))]
    public async Task LifetimeHasNullDatesAndRejectsMonths()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        using var response = await test.GrantAsync(new { studentId = test.Student, courseId = test.Course, accessPeriod = new { type = "lifetime" }, reason = "Partnership" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var grant = (await response.Content.ReadFromJsonAsync<CourtesyGrant>(Cancellation))!; Assert.Null(grant.EndsOn); Assert.Null(grant.ExpiresAt);
        using var invalid = await test.GrantAsync(new { studentId = test.Student, courseId = test.Course, accessPeriod = new { type = "lifetime", months = 6 }, reason = "Partnership" }, "new"); await AssertCodeAsync(invalid, 422, "FIELD_INVALID");
        using var explicitNull = await test.GrantAsync(new { studentId = test.Student, courseId = test.Course, accessPeriod = new { type = "lifetime", months = (int?)null }, reason = "Partnership" }, "null-months");
        await AssertCodeAsync(explicitNull, 422, "FIELD_INVALID");
    }
    [Fact(DisplayName = nameof(FailureAfterSaveBeforeCommitRollsBackAllFourTables))]
    public async Task FailureAfterSaveBeforeCommitRollsBackAllFourTables()
    {
        await using var test = new CourtesyGrantFixture(infra, services => { services.RemoveAll<IUnitOfWork>(); services.AddScoped<IUnitOfWork, CourtesyFailingCommit>(); }); await test.SeedAsync();
        using var response = await test.GrantAsync(); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); await test.AssertEmptyAsync();
    }
    [Fact(DisplayName = nameof(PermissionIsRequiredAndGetHidesOtherTenantsAndUnknownGrants))]
    public async Task PermissionIsRequiredAndGetHidesOtherTenantsAndUnknownGrants()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); test.Authorize(permission: "financeiro.ler");
        using var forbidden = await test.GrantAsync(); await AssertCodeAsync(forbidden, 403, "PERMISSION_DENIED"); await test.AssertEmptyAsync();
        test.Authorize(); using var granted = await test.GrantAsync(); var grant = (await granted.Content.ReadFromJsonAsync<CourtesyGrant>(Cancellation))!;
        await using (var scope = test.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.IgnoreQueryFilters().Where(item => item.Id == grant.GrantId).ExecuteUpdateAsync(update => update.SetProperty(item => item.Origin, "purchase"), Cancellation);
        using var otherOrigin = await test.Client.GetAsync($"/internal/v1/courtesy-grants/{grant.GrantId}", Cancellation); await AssertCodeAsync(otherOrigin, 404, "GRANT_NOT_FOUND");
        test.Authorize(Guid.CreateVersion7()); using var hidden = await test.Client.GetAsync($"/internal/v1/courtesy-grants/{grant.GrantId}", Cancellation); await AssertCodeAsync(hidden, 404, "GRANT_NOT_FOUND");
        using var unknown = await test.Client.GetAsync($"/internal/v1/courtesy-grants/{Guid.CreateVersion7()}", Cancellation); await AssertCodeAsync(unknown, 404, "GRANT_NOT_FOUND");
    }
    [Fact(DisplayName = nameof(ExpiredReceiptAllowsNewIntentAndHourlyCleanupDeletesOnlyExpiredRows))]
    public async Task ExpiredReceiptAllowsNewIntentAndHourlyCleanupDeletesOnlyExpiredRows()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); using var first = await test.GrantAsync(); test.Clock.Now = test.Clock.Now.AddHours(25);
        using var second = await test.GrantAsync(test.Body("New intent")); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var third = await test.GrantAsync(key: "expired-key"); test.Clock.Now = test.Clock.Now.AddHours(25);
        using var fourth = await test.GrantAsync(key: "retained-key");
        var cleaner = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<CodeForCoders.Commerce.Infra.Data.Entitlement.GrantReceiptCleanupWorker>(test.Factory.Services);
        await cleaner.CleanupAsync(Cancellation);
        await using var scope = test.Factory.Services.CreateAsyncScope();
        var receipts = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().GrantReceipts.IgnoreQueryFilters().Where(item => item.TenantId == test.Tenant).ToListAsync(Cancellation);
        Assert.Single(receipts); Assert.Equal(test.Clock.Now.AddHours(24), receipts[0].ExpiresAt);
    }
    private static async Task AssertCodeAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode); Assert.Contains(code, await response.Content.ReadAsStringAsync(Cancellation));
    }
}
