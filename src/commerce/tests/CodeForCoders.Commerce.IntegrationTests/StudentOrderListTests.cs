using System.Net;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class StudentOrderListTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture()
    {
        var tenant = Guid.CreateVersion7();
        return new(hosts.Showcase(tenant), tenant);
    }

    [Fact(DisplayName = nameof(ListReturnsThreeStatesInDescendingCreationOrder))]
    public async Task ListReturnsThreeStatesInDescendingCreationOrder()
    {
        var f = Fixture();
        var orders = await SeedAsync(f);
        using var client = f.Client();
        using var response = await client.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.True(response.Headers.CacheControl.NoStore);
        var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "StudentOrderPage");
        var rows = body.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(orders.Reverse().Select(order => order.Id), rows.Select(row => row.GetProperty("orderId").GetGuid()));
        Assert.Equal(new[] { "awaiting-payment", "expired", "paid" }, rows.Select(row => row.GetProperty("status").GetString()));
        Assert.Equal("boleto", rows[0].GetProperty("pendingPayment").GetProperty("method").GetString());
        Assert.Equal(3, body.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(10, body.GetProperty("pagination").GetProperty("size").GetInt32());
    }

    [Fact(DisplayName = nameof(PaginationHasStableIdTieBreakAndExactMetadata))]
    public async Task PaginationHasStableIdTieBreakAndExactMetadata()
    {
        var f = Fixture();
        var orders = await SeedAsync(f, sameDate: true);
        var expected = orders.OrderByDescending(order => order.Id).ToArray();
        using var client = f.Client();
        for (var page = 1; page <= 3; page++)
        {
            using var response = await client.GetAsync($"/internal/v1/orders?_page={page}&_size=1", OrderFixture.Cancellation);
            var body = await OrderFixture.BodyAsync(response);
            OrderHttpContract.AssertValid(body, "StudentOrderPage");
            Assert.Equal(expected[page - 1].Id, Assert.Single(body.GetProperty("data").EnumerateArray()).GetProperty("orderId").GetGuid());
            var pagination = body.GetProperty("pagination");
            Assert.Equal(page, pagination.GetProperty("page").GetInt32());
            Assert.Equal(1, pagination.GetProperty("size").GetInt32());
            Assert.Equal(3, pagination.GetProperty("total").GetInt32());
            Assert.Equal(3, pagination.GetProperty("totalPages").GetInt32());
        }
    }

    [Fact(DisplayName = nameof(OtherStudentsAndSchoolsAreExcludedFromRowsAndTotal))]
    public async Task OtherStudentsAndSchoolsAreExcludedFromRowsAndTotal()
    {
        var f = Fixture();
        var orders = await SeedAsync(f);
        var otherTenant = Guid.CreateVersion7();
        var otherSchool = new OrderFixture(hosts.Showcase(otherTenant), otherTenant);
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            db.Orders.Add(NewOrder(f.Tenant, Guid.CreateVersion7(), 4, DateTimeOffset.UtcNow));
            db.Orders.Add(NewOrder(otherTenant, f.Student, 5, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(OrderFixture.Cancellation);
        }
        using var client = f.Client();
        using var response = await client.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        var body = await OrderFixture.BodyAsync(response);
        Assert.Equal(orders.Reverse().Select(order => order.Id), body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("orderId").GetGuid()));
        Assert.Equal(3, body.GetProperty("pagination").GetProperty("total").GetInt32());
        using var otherClient = otherSchool.Client(student: f.Student);
        using var otherResponse = await otherClient.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        var otherBody = await OrderFixture.BodyAsync(otherResponse);
        Assert.Single(otherBody.GetProperty("data").EnumerateArray());
        Assert.Equal(1, otherBody.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = nameof(ChangedUnpublishedOfferKeepsTheFrozenOrderConditions))]
    public async Task ChangedUnpublishedOfferKeepsTheFrozenOrderConditions()
    {
        var f = Fixture();
        var ids = await f.SeedAsync();
        using var created = await f.CreateAsync(ids[1], "list-snapshot");
        var original = await OrderFixture.BodyAsync(created);
        await using (var scope = f.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var course = await db.CatalogCourseViews.Include(row => row.Offers).SingleAsync(row => row.CourseId == ids[0], OrderFixture.Cancellation);
            course.UpdateOffer(ids[1], new("Changed offer", 59700, AccessPeriod.Create("months", 6)), DateTimeOffset.UtcNow);
            course.UnpublishOffer(ids[1], DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(OrderFixture.Cancellation);
        }
        using var client = f.Client();
        using var response = await client.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "StudentOrderPage");
        var row = Assert.Single(body.GetProperty("data").EnumerateArray());
        foreach (var field in new[] { "orderId", "course", "offer", "priceCents", "currency", "accessPeriod" })
            Assert.Equal(original.GetProperty(field).GetRawText(), row.GetProperty(field).GetRawText());
    }

    [Fact(DisplayName = nameof(NoOrdersReturnsAnEmptyPage))]
    public async Task NoOrdersReturnsAnEmptyPage()
    {
        var f = Fixture();
        using var client = f.Client();
        using var response = await client.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "StudentOrderPage");
        Assert.Empty(body.GetProperty("data").EnumerateArray());
        Assert.Equal(0, body.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(0, body.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Fact(DisplayName = nameof(ActorTokenCannotListStudentOrders))]
    public async Task ActorTokenCannotListStudentOrders()
    {
        using var client = Fixture().Client(actor: true);
        using var response = await client.GetAsync("/internal/v1/orders", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory(DisplayName = nameof(InvalidPaginationReturns400))]
    [InlineData("_page=0")]
    [InlineData("_size=0")]
    [InlineData("_size=51")]
    [InlineData("_page=2147483647&_size=50")]
    public async Task InvalidPaginationReturns400(string query)
    {
        using var client = Fixture().Client();
        using var response = await client.GetAsync($"/internal/v1/orders?{query}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Order[]> SeedAsync(OrderFixture f, bool sameDate = false)
    {
        var start = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var orders = Enumerable.Range(1, 3).Select(number => NewOrder(f.Tenant, f.Student, number, sameDate ? start : start.AddDays(number))).ToArray();
        orders[0].ConfirmPayment(new("card", 49700, "BRL", "pi_list", start.AddDays(1)));
        orders[1].Expire(start.AddDays(3));
        orders[2].RecordPendingPayment("boleto", "pi_list_boleto", start.AddDays(8));
        await using var scope = f.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync(OrderFixture.Cancellation);
        return orders;
    }

    private static Order NewOrder(Guid tenant, Guid student, long number, DateTimeOffset createdAt) => Order.Create(tenant, student,
        new(number, new(Guid.CreateVersion7(), "Frozen course", Guid.CreateVersion7(), "12 months", 49700, "months", 12), createdAt));
}
