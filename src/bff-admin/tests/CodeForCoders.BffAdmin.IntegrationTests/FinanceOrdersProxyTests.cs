using System.Net;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.Clients;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class FinanceOrdersProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static FinanceOrdersBffApiFactory Factory()
    { var factory = new FinanceOrdersBffApiFactory(); factory.Identity.Roles = ["financeiro"]; factory.Identity.Permissions = ["financeiro.ler"]; return factory; }
    [Fact(DisplayName = nameof(PageComposesStudentsOnceWithSignedAssertionAndKeepsPersonalDataOutOfLogs))]
    public async Task PageComposesStudentsOnceWithSignedAssertionAndKeepsPersonalDataOutOfLogs()
    {
        await using var f = Factory(); using var client = await f.AuthenticatedAsync();
        Assert.IsType<CommerceFinanceAreaClient>(f.Services.GetRequiredService<ICommerceFinanceAreaClient>());
        Assert.IsType<StudentAccountIdentityClient>(f.Services.GetRequiredService<IStudentAccountIdentityClient>());
        using var response = await client.GetAsync($"/api/v1/finance/orders?_page=2&_size=20&status=paid&studentId={FinanceOrdersHttpHandler.Student}&createdFrom=2026-10-01", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await BodyAsync(response); var rows = body.GetProperty("data").EnumerateArray().ToArray(); Assert.Equal(2, rows.Length);
        Assert.All(rows, row => { Assert.Equal("Finance Student", row.GetProperty("student").GetProperty("name").GetString()); Assert.Equal(FinanceStudentsHttpHandler.Email, row.GetProperty("student").GetProperty("email").GetString()); Assert.False(row.TryGetProperty("studentId", out _)); });
        Assert.Equal(1, f.Students.Calls); Assert.Equal(FinanceOrdersHttpHandler.Student, Assert.Single(f.Students.StudentIds!));
        Assert.Equal("commerce", f.Identity.LastAudience); Assert.Equal("server-commerce-token", f.Orders.Token); Assert.Contains("status=paid", f.Orders.Uri!.Query);
        Assert.Contains("_page=2", f.Orders.Uri.Query); Assert.Contains("createdFrom=2026-10-01", f.Orders.Uri.Query);
        var session = await f.Sessions.GetAsync("opaque-course-session", Cancellation); Assert.Equal(session!.IdentitySessionId.ToString(), f.Students.Session);
        var parts = f.Students.Assertion!.Split('.'); var encoded = parts[1].Replace('-', '+').Replace('_', '/');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '=')));
        Assert.Equal("student-account:resolve", claims.RootElement.GetProperty("scope").GetString()); Assert.Equal("bff-admin", claims.RootElement.GetProperty("iss").GetString());
        Assert.All(f.Logs, log => { Assert.DoesNotContain(FinanceStudentsHttpHandler.Email, log); Assert.DoesNotContain("Finance Student", log); });
    }
    [Fact(DisplayName = nameof(DetailKeepsReferenceConfirmedAmountAndGrantAndUnknownOrderIs404))]
    public async Task DetailKeepsReferenceConfirmedAmountAndGrantAndUnknownOrderIs404()
    {
        await using var f = Factory(); using var client = await f.AuthenticatedAsync();
        using var response = await client.GetAsync($"/api/v1/finance/orders/{FinanceOrdersHttpHandler.Order}", Cancellation); var body = await BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("pi_proxy", body.GetProperty("paymentReference").GetString());
        Assert.Equal(39700, body.GetProperty("paidAmountCents").GetInt32()); Assert.NotEqual(Guid.Empty, body.GetProperty("grantId").GetGuid());
        f.Orders.Status = HttpStatusCode.NotFound;
        using var missing = await client.GetAsync($"/api/v1/finance/orders/{Guid.CreateVersion7()}", Cancellation); await ProblemAsync(missing, HttpStatusCode.NotFound, "ORDER_NOT_FOUND");
        Assert.Equal(1, f.Students.Calls);
    }
    [Fact(DisplayName = nameof(OtherRolesAndRevokedPermissionsCannotReadAnOpenSession))]
    public async Task OtherRolesAndRevokedPermissionsCannotReadAnOpenSession()
    {
        await using var f = Factory(); using var client = await f.AuthenticatedAsync();
        using var first = await client.GetAsync("/api/v1/finance/orders", Cancellation); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        foreach (var role in new[] { "professor", "suporte", "administrador", "aluno" })
        {
            f.Identity.Roles = [role]; f.Identity.Permissions = [];
            foreach (var path in new[] { "/api/v1/finance/orders", $"/api/v1/finance/orders/{FinanceOrdersHttpHandler.Order}" })
            { using var response = await client.GetAsync(path, Cancellation); await ProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED"); }
        }
        Assert.Equal(1, f.Orders.Calls); Assert.Equal(1, f.Students.Calls);
    }
    [Fact(DisplayName = nameof(IdentityUnavailableOrOmittedStudentNeverReturnsPartialPage))]
    public async Task IdentityUnavailableOrOmittedStudentNeverReturnsPartialPage()
    {
        await using var f = Factory(); using var client = await f.AuthenticatedAsync(); f.Students.Unavailable = true;
        using var response = await client.GetAsync("/api/v1/finance/orders", Cancellation); await ProblemAsync(response, HttpStatusCode.BadGateway, "IDENTITY_UNAVAILABLE");
        f.Students.Unavailable = false; f.Students.Missing = true;
        using var missing = await client.GetAsync("/api/v1/finance/orders", Cancellation); await ProblemAsync(missing, HttpStatusCode.BadGateway, "IDENTITY_UNAVAILABLE");
        Assert.False((await BodyAsync(missing)).TryGetProperty("data", out _));
    }
    [Fact(DisplayName = nameof(RevokedSessionAndSecondValidationPermissionRemovalStopCommerce))]
    public async Task RevokedSessionAndSecondValidationPermissionRemovalStopCommerce()
    {
        await using var f = Factory(); using var client = await f.AuthenticatedAsync(); f.Identity.CommercePermissions = [];
        using var role = await client.GetAsync("/api/v1/finance/orders", Cancellation); await ProblemAsync(role, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        f.Identity.Revoked = true;
        using var session = await client.GetAsync("/api/v1/finance/orders", Cancellation); await ProblemAsync(session, HttpStatusCode.Unauthorized, "SESSION_REQUIRED"); Assert.Equal(0, f.Orders.Calls);
    }
    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    { using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)); return doc.RootElement.Clone(); }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await BodyAsync(response)).GetProperty("code").GetString()); Assert.True(response.Headers.CacheControl!.NoStore); }
}
