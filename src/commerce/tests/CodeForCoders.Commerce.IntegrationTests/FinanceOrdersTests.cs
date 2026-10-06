using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class FinanceOrdersTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private OrderFixture Fixture() { var tenant = Guid.CreateVersion7(); return new(hosts.Showcase(tenant), tenant); }
    private static Order Order(OrderFixture f, long number, DateTimeOffset date, Guid? student = null, Guid? course = null)
        => CodeForCoders.Commerce.Domain.Entities.Order.Create(f.Tenant, student ?? f.Student,
            new(number, new(course ?? Guid.CreateVersion7(), "Frozen course", Guid.CreateVersion7(), "12 months", 49700, "months", 12), date));
    private static async Task SaveAsync(OrderFixture f, params Order[] orders)
    {
        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); db.Orders.AddRange(orders); await db.SaveChangesAsync(Cancellation);
    }
    [Fact(DisplayName = nameof(CombinedFiltersUseInclusiveSchoolDates))]
    public async Task CombinedFiltersUseInclusiveSchoolDates()
    {
        var f = Fixture(); var course = Guid.CreateVersion7();
        var start = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);
        var first = Order(f, 1, start, course: course); var last = Order(f, 2, start.AddDays(1).AddTicks(-10), course: course);
        var before = Order(f, 3, start.AddTicks(-10), course: course); var after = Order(f, 4, start.AddDays(1), course: course);
        var paid = Order(f, 5, start.AddHours(1), course: course); paid.ConfirmPayment(new("card", 49700, "BRL", "pi_filter", start));
        await SaveAsync(f, first, last, before, after, paid, Order(f, 6, start, Guid.CreateVersion7(), course), Order(f, 7, start));
        using var client = f.Client(actor: true);
        using var response = await client.GetAsync($"/internal/v1/finance/orders?status=awaiting-payment&courseId={course}&studentId={f.Student}&createdFrom=2026-10-01&createdTo=2026-10-01", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "FinanceOrderPage"); Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(new[] { last.Id, first.Id }, body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("orderId").GetGuid()));
        Assert.Equal(2, body.GetProperty("pagination").GetProperty("total").GetInt32());
    }
    [Fact(DisplayName = nameof(InvertedPeriodReturns400))]
    public async Task InvertedPeriodReturns400()
    {
        using var client = Fixture().Client(actor: true);
        using var response = await client.GetAsync("/internal/v1/finance/orders?createdFrom=2026-10-05&createdTo=2026-10-01", Cancellation);
        await ProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
    }
    [Fact(DisplayName = nameof(DetailExposesPaymentDiscrepancyAndPurchaseGrant))]
    public async Task DetailExposesPaymentDiscrepancyAndPurchaseGrant()
    {
        var f = Fixture(); var now = DateTimeOffset.UtcNow; var order = Order(f, 1, now);
        order.OpenPayment(now.AddDays(1)); order.ConfirmPayment(new("card", 39700, "BRL", "pi_finance", now)); order.RecordAccess(Guid.CreateVersion7(), now);
        await SaveAsync(f, order); using var client = f.Client(actor: true);
        using var response = await client.GetAsync($"/internal/v1/finance/orders/{order.Id}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "FinanceOrderDetail"); Assert.Equal("pi_finance", body.GetProperty("paymentReference").GetString());
        Assert.Equal(39700, body.GetProperty("paidAmountCents").GetInt32()); Assert.Equal(order.GrantId, body.GetProperty("grantId").GetGuid());
        Assert.Equal(12, body.GetProperty("accessPeriod").GetProperty("months").GetInt32());
    }
    [Fact(DisplayName = nameof(OtherSchoolIsExcludedFromRowsAndDetail))]
    public async Task OtherSchoolIsExcludedFromRowsAndDetail()
    {
        var f = Fixture(); var other = Fixture(); var own = Order(f, 1, DateTimeOffset.UtcNow); var foreign = Order(other, 1, DateTimeOffset.UtcNow);
        await SaveAsync(f, own); await SaveAsync(other, foreign); using var client = f.Client(actor: true);
        using var list = await client.GetAsync("/internal/v1/finance/orders", Cancellation); var body = await OrderFixture.BodyAsync(list);
        Assert.Equal(own.Id, Assert.Single(body.GetProperty("data").EnumerateArray()).GetProperty("orderId").GetGuid());
        using var detail = await client.GetAsync($"/internal/v1/finance/orders/{foreign.Id}", Cancellation); await ProblemAsync(detail, HttpStatusCode.NotFound, "ORDER_NOT_FOUND");
    }
    [Fact(DisplayName = nameof(TeacherSupportAndAdministratorCannotReadEitherEndpoint))]
    public async Task TeacherSupportAndAdministratorCannotReadEitherEndpoint()
    {
        var f = Fixture();
        foreach (var role in new[] { "professor", "suporte", "administrador" })
        {
            using var client = f.Factory.CreateClient();
            var token = new JwtSecurityToken("identity", "commerce", [new Claim("sub", Guid.CreateVersion7().ToString()), new Claim("tenantId", f.Tenant.ToString()), new Claim("roles", role), new Claim("permissions", "autoria.ler")],
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(f.Factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            foreach (var path in new[] { "/internal/v1/finance/orders", $"/internal/v1/finance/orders/{Guid.CreateVersion7()}" })
            { using var response = await client.GetAsync(path, Cancellation); await ProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED"); }
        }
    }
    [Fact(DisplayName = nameof(StudentTokenCannotReadFinanceOrders))]
    public async Task StudentTokenCannotReadFinanceOrders()
    {
        using var client = Fixture().Client();
        foreach (var path in new[] { "/internal/v1/finance/orders", $"/internal/v1/finance/orders/{Guid.CreateVersion7()}" })
        { using var response = await client.GetAsync(path, Cancellation); await ProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED"); }
    }
    [Fact(DisplayName = nameof(PagesHaveStableOrderingAndCompleteCounts))]
    public async Task PagesHaveStableOrderingAndCompleteCounts()
    {
        var f = Fixture(); var now = DateTimeOffset.UtcNow; var rows = new[] { Order(f, 1, now), Order(f, 2, now), Order(f, 3, now, Guid.CreateVersion7()) };
        await SaveAsync(f, rows); using var client = f.Client(actor: true);
        using var response = await client.GetAsync("/internal/v1/finance/orders?_page=2&_size=1", Cancellation); var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "FinanceOrderPage"); Assert.Equal(rows.OrderByDescending(row => row.Id).ElementAt(1).Id, Assert.Single(body.GetProperty("data").EnumerateArray()).GetProperty("orderId").GetGuid());
        Assert.Equal(3, body.GetProperty("pagination").GetProperty("total").GetInt32()); Assert.Equal(3, body.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }
    [Theory(DisplayName = nameof(InvalidFiltersAreRejected))]
    [InlineData("status=invalid")]
    [InlineData("_page=0")]
    [InlineData("_size=51")]
    [InlineData("_page=2147483647&_size=50")]
    public async Task InvalidFiltersAreRejected(string query)
    { using var client = Fixture().Client(actor: true); using var response = await client.GetAsync($"/internal/v1/finance/orders?{query}", Cancellation); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await OrderFixture.BodyAsync(response)).GetProperty("code").GetString()); }
}
