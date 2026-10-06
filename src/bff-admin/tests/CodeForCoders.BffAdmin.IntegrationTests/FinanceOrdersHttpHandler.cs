using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Api.Clients;
namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class FinanceOrdersHttpHandler : HttpMessageHandler
{
    public static Guid Student { get; } = Guid.CreateVersion7();
    public static Guid Order { get; } = Guid.CreateVersion7();
    public int Calls { get; private set; }
    public string? Token { get; private set; }
    public Uri? Uri { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Code { get; set; } = "ORDER_NOT_FOUND";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; Token = request.Headers.Authorization?.Parameter; Uri = request.RequestUri;
        if (Status != HttpStatusCode.OK) return Task.FromResult(new HttpResponseMessage(Status) { Content = JsonContent.Create(new { code = Code }) });
        var now = DateTimeOffset.UtcNow; var course = Guid.CreateVersion7();
        var row = new CommerceFinanceOrderSummary(Order, "000123", Student, "paid", course, "Frozen course", "12 months", 49700, "BRL", "card", now, now);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Uri!.AbsolutePath.EndsWith(Order.ToString(), StringComparison.Ordinal)
                ? JsonContent.Create(new CommerceFinanceOrderDetail(Order, "000123", Student, "paid", course, "Frozen course", Guid.CreateVersion7(), "12 months", 49700, "BRL", new FinanceOrderPeriod("months", 12), "card", "pi_proxy", 39700, Guid.CreateVersion7(), now, now, null, now, null, null))
                : JsonContent.Create(new CommerceFinanceOrderPage([row, row with { OrderId = Guid.CreateVersion7(), Number = "000124" }], new(2, 20, 22, 2)))
        });
    }
}
